using System.Globalization;
using System.Text;
using System.Text.Json;
using FinanceTracker.Application.Statements.Dtos;
using FinanceTracker.Application.Statements.Parsing;

namespace FinanceTracker.Importing.Csv;

/// <summary>
/// Deterministic, isolated CSV statement parser.
/// Parses CSV statements according to RFC 4180 streaming rules.
/// Completely read-only: does not interact with the database or mutate any transactions.
/// </summary>
public class CsvStatementParser : ICsvStatementParser
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "dd.MM.yyyy",
        "yyyy.MM.dd",
        "dd MMM yyyy",
        "dd-MMM-yyyy",
        "dd-MMM-yy",
        "dd/MMM/yyyy"
    ];

    private static readonly HashSet<string> DateHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "date", "transaction date", "txn date", "trans date", "value date", "posting date", "booking date"
    };

    private static readonly HashSet<string> DescriptionHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "description", "narrative", "details", "particulars", "memo", "payee", "transaction details", "remarks"
    };

    private static readonly HashSet<string> DebitHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "debit", "withdrawal", "dr", "debit amount", "paid out", "spend", "withdrawals"
    };

    private static readonly HashSet<string> CreditHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "credit", "deposit", "cr", "credit amount", "paid in", "receive", "deposits"
    };

    private static readonly HashSet<string> AmountHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "amount", "transaction amount", "txn amount", "net amount"
    };

    private static readonly HashSet<string> TypeHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "type", "dr/cr", "cr/dr", "direction", "txn type", "transaction type"
    };

    private static readonly HashSet<string> BalanceHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "balance", "running balance", "account balance", "closing balance"
    };

    public async Task<CsvParseResult> ParseAsync(Stream csvStream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        var rows = new List<ParsedTransactionRowDto>();
        var errors = new List<StatementParseErrorDto>();

        // Ensure stream is at beginning if seekable
        if (csvStream.CanSeek)
        {
            csvStream.Seek(0, SeekOrigin.Begin);
        }

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

        // Read records using RFC 4180 streaming record reader
        var records = ReadCsvRecords(reader).GetAsyncEnumerator(ct);

        if (!await records.MoveNextAsync())
        {
            errors.Add(new StatementParseErrorDto(null, "CSV file is empty."));
            return new CsvParseResult(false, 0, rows, errors, null, null);
        }

        var headerRecord = records.Current;
        var headerMapping = AnalyzeHeaders(headerRecord);

        if (!headerMapping.IsValid)
        {
            errors.Add(new StatementParseErrorDto(1, $"Unsupported CSV format: {headerMapping.ErrorMessage}"));
            return new CsvParseResult(false, 0, rows, errors, null, null);
        }

        int rowNumber = 1; // Row 1 is header
        int totalRows = 0;

        while (await records.MoveNextAsync())
        {
            rowNumber++;
            var fields = records.Current;

            // Skip trailing empty lines
            if (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]))
            {
                continue;
            }

            totalRows++;

            if (fields.Count < headerMapping.MinRequiredColumns)
            {
                errors.Add(new StatementParseErrorDto(rowNumber,
                    $"Malformed row: expected at least {headerMapping.MinRequiredColumns} columns but found {fields.Count}."));
                continue;
            }

            // 1. Parse Date
            var dateStr = GetField(fields, headerMapping.DateIndex);
            if (!TryParseDate(dateStr, out var parsedDate))
            {
                errors.Add(new StatementParseErrorDto(rowNumber,
                    $"Invalid date format in '{dateStr}'."));
                continue;
            }

            // 2. Parse Description
            var descStr = GetField(fields, headerMapping.DescriptionIndex);
            if (string.IsNullOrWhiteSpace(descStr))
            {
                errors.Add(new StatementParseErrorDto(rowNumber, "Description is empty."));
                continue;
            }
            var sanitizedDesc = SanitizeForFormulaInjection(descStr);

            // 3. Parse Amount and Direction
            decimal finalAmount;
            string direction;

            if (headerMapping.DebitIndex.HasValue && headerMapping.CreditIndex.HasValue)
            {
                var debitStr = GetField(fields, headerMapping.DebitIndex.Value);
                var creditStr = GetField(fields, headerMapping.CreditIndex.Value);

                var hasDebit = TryParseDecimal(debitStr, out var debitVal) && debitVal > 0;
                var hasCredit = TryParseDecimal(creditStr, out var creditVal) && creditVal > 0;

                if (hasDebit && hasCredit)
                {
                    errors.Add(new StatementParseErrorDto(rowNumber,
                        $"Ambiguous debit/credit: row contains both debit ({debitVal}) and credit ({creditVal}) amounts."));
                    continue;
                }

                if (!hasDebit && !hasCredit)
                {
                    // Check if zero amounts or unparseable
                    if (TryParseDecimal(debitStr, out var dZero) && dZero == 0 &&
                        TryParseDecimal(creditStr, out var cZero) && cZero == 0)
                    {
                        errors.Add(new StatementParseErrorDto(rowNumber, "Transaction amount is zero in both debit and credit columns."));
                    }
                    else
                    {
                        errors.Add(new StatementParseErrorDto(rowNumber, $"Invalid debit ('{debitStr}') or credit ('{creditStr}') amount."));
                    }
                    continue;
                }

                if (hasDebit)
                {
                    finalAmount = debitVal;
                    direction = "Debit";
                }
                else
                {
                    finalAmount = creditVal;
                    direction = "Credit";
                }
            }
            else if (headerMapping.AmountIndex.HasValue)
            {
                var amountStr = GetField(fields, headerMapping.AmountIndex.Value);
                if (!TryParseDecimal(amountStr, out var parsedAmt))
                {
                    errors.Add(new StatementParseErrorDto(rowNumber, $"Invalid transaction amount: '{amountStr}'."));
                    continue;
                }

                if (parsedAmt == 0)
                {
                    errors.Add(new StatementParseErrorDto(rowNumber, "Transaction amount cannot be zero."));
                    continue;
                }

                if (headerMapping.TypeIndex.HasValue)
                {
                    var typeStr = GetField(fields, headerMapping.TypeIndex.Value)?.Trim().ToUpperInvariant();
                    if (typeStr == "DR" || typeStr == "DEBIT" || typeStr == "W")
                    {
                        direction = "Debit";
                        finalAmount = Math.Abs(parsedAmt);
                    }
                    else if (typeStr == "CR" || typeStr == "CREDIT" || typeStr == "D")
                    {
                        direction = "Credit";
                        finalAmount = Math.Abs(parsedAmt);
                    }
                    else
                    {
                        // Infer from sign if type is unrecognized
                        direction = parsedAmt < 0 ? "Debit" : "Credit";
                        finalAmount = Math.Abs(parsedAmt);
                    }
                }
                else
                {
                    direction = parsedAmt < 0 ? "Debit" : "Credit";
                    finalAmount = Math.Abs(parsedAmt);
                }
            }
            else
            {
                errors.Add(new StatementParseErrorDto(rowNumber, "Missing amount column."));
                continue;
            }

            // 4. Parse optional Balance
            decimal? parsedBalance = null;
            if (headerMapping.BalanceIndex.HasValue)
            {
                var balStr = GetField(fields, headerMapping.BalanceIndex.Value);
                if (!string.IsNullOrWhiteSpace(balStr) && TryParseDecimal(balStr, out var bal))
                {
                    parsedBalance = bal;
                }
            }

            rows.Add(new ParsedTransactionRowDto(
                rowNumber,
                parsedDate,
                sanitizedDesc,
                finalAmount,
                direction,
                parsedBalance));
        }

        var isSuccess = errors.Count == 0 && rows.Count > 0;
        var summary = new
        {
            totalRowsRead = totalRows,
            parsedRowsCount = rows.Count,
            errorCount = errors.Count,
            totalDebit = rows.Where(r => r.Direction == "Debit").Sum(r => r.Amount),
            totalCredit = rows.Where(r => r.Direction == "Credit").Sum(r => r.Amount),
            startDate = rows.Count > 0 ? rows.Min(r => r.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            endDate = rows.Count > 0 ? rows.Max(r => r.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            format = headerMapping.FormatName
        };

        return new CsvParseResult(
            isSuccess,
            totalRows,
            rows,
            errors,
            headerMapping.FormatName,
            JsonSerializer.Serialize(summary));
    }

    private static string? GetField(List<string> fields, int index)
    {
        return index >= 0 && index < fields.Count ? fields[index] : null;
    }

    private static bool TryParseDate(string? input, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim();
        if (DateOnly.TryParseExact(cleaned, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateOnly.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseDecimal(string? input, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = input.Trim()
            .Replace("$", "")
            .Replace("€", "")
            .Replace("£", "")
            .Replace("₹", "")
            .Replace("Rs.", "")
            .Replace("Rs", "")
            .Replace(",", "")
            .Trim();

        // Handle negative numbers enclosed in parentheses e.g. (100.50)
        if (cleaned.StartsWith('(') && cleaned.EndsWith(')'))
        {
            cleaned = "-" + cleaned[1..^1].Trim();
        }

        return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);
    }

    private static string SanitizeForFormulaInjection(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim();
        // Prevent CSV / Excel formula injection triggers (=, +, -, @, tab, cr)
        if (trimmed.StartsWith('=') || trimmed.StartsWith('+') || trimmed.StartsWith('@') || trimmed.StartsWith('\t'))
        {
            trimmed = "'" + trimmed;
        }

        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static HeaderMappingResult AnalyzeHeaders(List<string> headers)
    {
        int? dateIdx = null;
        int? descIdx = null;
        int? debitIdx = null;
        int? creditIdx = null;
        int? amountIdx = null;
        int? typeIdx = null;
        int? balanceIdx = null;

        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i].Trim();
            if (dateIdx == null && DateHeaderKeywords.Contains(header))
            {
                dateIdx = i;
            }
            else if (descIdx == null && DescriptionHeaderKeywords.Contains(header))
            {
                descIdx = i;
            }
            else if (debitIdx == null && DebitHeaderKeywords.Contains(header))
            {
                debitIdx = i;
            }
            else if (creditIdx == null && CreditHeaderKeywords.Contains(header))
            {
                creditIdx = i;
            }
            else if (amountIdx == null && AmountHeaderKeywords.Contains(header))
            {
                amountIdx = i;
            }
            else if (typeIdx == null && TypeHeaderKeywords.Contains(header))
            {
                typeIdx = i;
            }
            else if (balanceIdx == null && BalanceHeaderKeywords.Contains(header))
            {
                balanceIdx = i;
            }
        }

        if (dateIdx == null)
        {
            return HeaderMappingResult.Failure("Missing required 'Date' column in CSV header.");
        }

        if (descIdx == null)
        {
            return HeaderMappingResult.Failure("Missing required 'Description' column in CSV header.");
        }

        var hasDebitCredit = debitIdx != null && creditIdx != null;
        var hasAmount = amountIdx != null;

        if (!hasDebitCredit && !hasAmount)
        {
            return HeaderMappingResult.Failure("Missing required amount columns. CSV must contain either separate 'Debit' and 'Credit' columns or a single 'Amount' column.");
        }

        string formatName = hasDebitCredit ? "Generic Csv (Debit/Credit)" : "Generic Csv (Single Amount)";
        int minRequired = new[] { dateIdx.Value, descIdx.Value, debitIdx ?? -1, creditIdx ?? -1, amountIdx ?? -1 }.Max() + 1;

        return HeaderMappingResult.Success(
            dateIdx.Value,
            descIdx.Value,
            debitIdx,
            creditIdx,
            amountIdx,
            typeIdx,
            balanceIdx,
            formatName,
            minRequired);
    }

    /// <summary>
    /// RFC 4180 streaming reader that parses records without loading the entire file into memory.
    /// Handles quoted fields with embedded commas, quotes, and newlines.
    /// </summary>
    private static async IAsyncEnumerable<List<string>> ReadCsvRecords(StreamReader reader)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        int nextChar;

        while ((nextChar = reader.Read()) != -1)
        {
            char c = (char)nextChar;

            if (inQuotes)
            {
                if (c == '"')
                {
                    int peek = reader.Peek();
                    if (peek == '"')
                    {
                        // Escaped quote ("")
                        reader.Read();
                        currentField.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else if (c == '\r')
                {
                    if (reader.Peek() == '\n')
                    {
                        reader.Read();
                    }

                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                    yield return fields;
                    fields = new List<string>();
                }
                else if (c == '\n')
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                    yield return fields;
                    fields = new List<string>();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        if (currentField.Length > 0 || fields.Count > 0)
        {
            fields.Add(currentField.ToString().Trim());
            yield return fields;
        }
    }

    private sealed class HeaderMappingResult
    {
        public bool IsValid { get; private init; }
        public string? ErrorMessage { get; private init; }
        public int DateIndex { get; private init; }
        public int DescriptionIndex { get; private init; }
        public int? DebitIndex { get; private init; }
        public int? CreditIndex { get; private init; }
        public int? AmountIndex { get; private init; }
        public int? TypeIndex { get; private init; }
        public int? BalanceIndex { get; private init; }
        public string FormatName { get; private init; } = string.Empty;
        public int MinRequiredColumns { get; private init; }

        public static HeaderMappingResult Failure(string error) =>
            new() { IsValid = false, ErrorMessage = error };

        public static HeaderMappingResult Success(
            int dateIdx,
            int descIdx,
            int? debitIdx,
            int? creditIdx,
            int? amountIdx,
            int? typeIdx,
            int? balanceIdx,
            string formatName,
            int minCols) =>
            new()
            {
                IsValid = true,
                DateIndex = dateIdx,
                DescriptionIndex = descIdx,
                DebitIndex = debitIdx,
                CreditIndex = creditIdx,
                AmountIndex = amountIdx,
                TypeIndex = typeIdx,
                BalanceIndex = balanceIdx,
                FormatName = formatName,
                MinRequiredColumns = minCols
            };
    }
}
