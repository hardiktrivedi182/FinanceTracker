using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.ImportBatches.Services;

/// <summary>
/// Contract for calculating canonical deduplication fingerprints.
/// </summary>
public interface IImportFingerprintService
{
    string ComputeFingerprint(
        Guid accountId,
        DateOnly transactionDate,
        string description,
        decimal amount,
        TransactionType type,
        string currencyCode);

    string NormalizeDescription(string description);
}

public partial class ImportFingerprintService : IImportFingerprintService
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public string ComputeFingerprint(
        Guid accountId,
        DateOnly transactionDate,
        string description,
        decimal amount,
        TransactionType type,
        string currencyCode)
    {
        var normalizedDesc = NormalizeDescription(description);
        var normalizedAmount = Math.Abs(amount).ToString("F4", CultureInfo.InvariantCulture);
        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        var normalizedType = type.ToString().ToUpperInvariant();
        var dateStr = transactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var accountStr = accountId.ToString("D").ToLowerInvariant();

        var canonicalPayload = $"{accountStr}|{dateStr}|{normalizedDesc}|{normalizedAmount}|{normalizedType}|{normalizedCurrency}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));

        return Convert.ToHexStringLower(hashBytes);
    }

    public string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var trimmed = description.Trim();

        // Strip leading formula injection escaping or trigger chars
        while (trimmed.StartsWith('\'') || trimmed.StartsWith('=') || trimmed.StartsWith('+') || trimmed.StartsWith('-') || trimmed.StartsWith('@'))
        {
            trimmed = trimmed[1..].Trim();
        }

        // Collapse whitespace runs into a single space and convert to lowercase
        var collapsed = WhitespaceRegex().Replace(trimmed, " ");
        return collapsed.ToLowerInvariant();
    }
}
