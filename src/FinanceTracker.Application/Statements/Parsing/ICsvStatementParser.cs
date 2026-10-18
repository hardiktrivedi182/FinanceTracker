using FinanceTracker.Application.Statements.Dtos;

namespace FinanceTracker.Application.Statements.Parsing;

/// <summary>
/// Application abstraction for parsing statement files into structured transaction candidates
/// without mutating ledger state.
/// </summary>
public interface ICsvStatementParser
{
    Task<CsvParseResult> ParseAsync(Stream csvStream, CancellationToken ct = default);
}

public record CsvParseResult(
    bool IsSuccess,
    int TotalRows,
    IReadOnlyList<ParsedTransactionRowDto> Rows,
    IReadOnlyList<StatementParseErrorDto> Errors,
    string? FormatDetected,
    string? SummaryJson);
