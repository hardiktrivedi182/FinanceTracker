using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Domain entity representing an uploaded financial statement file metadata and processing state.
/// Raw document content is stored through the storage abstraction and never stored in the database.
/// </summary>
public class Statement
{
    // Parameterless constructor for EF Core
    private Statement()
    {
        UploaderUserId = string.Empty;
        OriginalFileName = string.Empty;
        StorageKey = string.Empty;
    }

    public Statement(
        Guid id,
        Guid householdId,
        string uploaderUserId,
        string originalFileName,
        StatementFormat format,
        long fileSizeBytes,
        string storageKey,
        DateTimeOffset uploadedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Statement Id cannot be empty.", nameof(id));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household Id cannot be empty.", nameof(householdId));
        }

        if (string.IsNullOrWhiteSpace(uploaderUserId))
        {
            throw new ArgumentException("Uploader user Id cannot be empty.", nameof(uploaderUserId));
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("Original filename cannot be empty.", nameof(originalFileName));
        }

        if (fileSizeBytes <= 0)
        {
            throw new ArgumentException("File size must be greater than zero.", nameof(fileSizeBytes));
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key cannot be empty.", nameof(storageKey));
        }

        Id = id;
        HouseholdId = householdId;
        UploaderUserId = uploaderUserId.Trim();
        OriginalFileName = SanitizeFileName(originalFileName);
        Format = format;
        FileSizeBytes = fileSizeBytes;
        StorageKey = storageKey.Trim();
        UploadedAt = uploadedAt;
        Status = StatementProcessingStatus.Uploaded;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string UploaderUserId { get; private set; }
    public string OriginalFileName { get; private set; }
    public StatementFormat Format { get; private set; }
    public long FileSizeBytes { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public string StorageKey { get; private set; }
    public StatementProcessingStatus Status { get; private set; }
    public int? ParsedTransactionCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ParseSummaryJson { get; private set; }

    public void MarkParsing()
    {
        Status = StatementProcessingStatus.Parsing;
        ErrorMessage = null;
    }

    public void MarkParsed(int parsedTransactionCount, string? parseSummaryJson = null)
    {
        if (parsedTransactionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parsedTransactionCount), "Transaction count cannot be negative.");
        }

        Status = StatementProcessingStatus.Parsed;
        ParsedTransactionCount = parsedTransactionCount;
        ParseSummaryJson = parseSummaryJson;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = StatementProcessingStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown error" : errorMessage.Trim();
    }

    public void MarkCommitted()
    {
        Status = StatementProcessingStatus.Committed;
    }

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "statement.csv";
        }

        var fileNameOnly = Path.GetFileName(input);
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = fileNameOnly
            .Where(c => !invalidChars.Contains(c) && !char.IsControl(c) && c != '/' && c != '\\')
            .ToArray();

        var sanitized = new string(sanitizedChars).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "statement.csv";
        }

        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}
