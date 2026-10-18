using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Domain entity representing a reviewable import workflow instance for an uploaded statement and target account.
/// Manages staging row candidates, ambiguity warnings, deduplication flags, and explicit user-confirmed commit.
/// </summary>
public class ImportBatch
{
    private readonly List<ImportStagingRow> _rows = [];

    // Parameterless constructor for EF Core
    private ImportBatch()
    {
        UploadedByUserId = string.Empty;
    }

    public ImportBatch(
        Guid id,
        Guid householdId,
        Guid statementId,
        Guid accountId,
        string uploadedByUserId,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Import batch Id cannot be empty.", nameof(id));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household Id cannot be empty.", nameof(householdId));
        }

        if (statementId == Guid.Empty)
        {
            throw new ArgumentException("Statement Id cannot be empty.", nameof(statementId));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account Id cannot be empty.", nameof(accountId));
        }

        if (string.IsNullOrWhiteSpace(uploadedByUserId))
        {
            throw new ArgumentException("Uploaded by user Id cannot be empty.", nameof(uploadedByUserId));
        }

        Id = id;
        HouseholdId = householdId;
        StatementId = statementId;
        AccountId = accountId;
        UploadedByUserId = uploadedByUserId.Trim();
        CreatedAt = createdAt;
        Status = ImportBatchStatus.AwaitingReview;
        TotalRowCount = 0;
        ReadyCount = 0;
        DuplicateCount = 0;
        ErrorCount = 0;
        SelectedRowCount = 0;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid StatementId { get; private set; }
    public Guid AccountId { get; private set; }
    public string UploadedByUserId { get; private set; }
    public ImportBatchStatus Status { get; private set; }

    public int TotalRowCount { get; private set; }
    public int ReadyCount { get; private set; }
    public int DuplicateCount { get; private set; }
    public int ErrorCount { get; private set; }
    public int SelectedRowCount { get; private set; }
    public int? CommittedCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }
    public string? CommittedByUserId { get; private set; }

    // Concurrency token for optimistic locking on commit
    public byte[]? RowVersion { get; private set; }

    // Navigation properties
    public Household Household { get; private set; } = null!;
    public Statement Statement { get; private set; } = null!;
    public Account Account { get; private set; } = null!;
    public IReadOnlyCollection<ImportStagingRow> Rows => _rows.AsReadOnly();

    public void AddRow(ImportStagingRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.ImportBatchId != Id)
        {
            throw new InvalidOperationException($"Row import batch Id '{row.ImportBatchId}' does not match batch Id '{Id}'.");
        }

        _rows.Add(row);
        RecalculateCountsAndStatus();
    }

    public void RecalculateCountsAndStatus()
    {
        TotalRowCount = _rows.Count;
        ErrorCount = _rows.Count(r => r.ValidationStatus == StagingRowValidationStatus.Invalid && r.Resolution != RowResolution.Excluded);
        DuplicateCount = _rows.Count(r => r.DuplicateStatus != DuplicateStatus.None);
        SelectedRowCount = _rows.Count(r => r.IsSelected);
        ReadyCount = _rows.Count(r => r.IsEligibleForCommit);

        if (Status == ImportBatchStatus.Committed)
        {
            // Committed status is terminal and immutable
            return;
        }

        if (Status == ImportBatchStatus.Cancelled)
        {
            return;
        }

        // Determine workflow status
        // Unresolved issues are invalid rows that have not been excluded, or duplicates with no resolution
        bool hasUnresolvedIssues = _rows.Any(r =>
            (r.ValidationStatus == StagingRowValidationStatus.Invalid && r.Resolution != RowResolution.Excluded) ||
            (r.DuplicateStatus != DuplicateStatus.None && r.Resolution == RowResolution.None));

        if (hasUnresolvedIssues)
        {
            Status = ImportBatchStatus.ReviewRequired;
        }
        else if (ReadyCount > 0 && SelectedRowCount > 0)
        {
            Status = ImportBatchStatus.ReadyToCommit;
        }
        else
        {
            Status = ImportBatchStatus.AwaitingReview;
        }
    }

    public void MarkCommitted(string committedByUserId, DateTimeOffset committedAt, int committedCount)
    {
        if (Status == ImportBatchStatus.Committed)
        {
            throw new InvalidOperationException("Import batch has already been committed.");
        }

        if (string.IsNullOrWhiteSpace(committedByUserId))
        {
            throw new ArgumentException("Committed by user Id cannot be empty.", nameof(committedByUserId));
        }

        if (committedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(committedCount), "Committed count cannot be negative.");
        }

        Status = ImportBatchStatus.Committed;
        CommittedByUserId = committedByUserId.Trim();
        CommittedAt = committedAt;
        CommittedCount = committedCount;
    }

    public void MarkCancelled()
    {
        if (Status == ImportBatchStatus.Committed)
        {
            throw new InvalidOperationException("Cannot cancel an already committed import batch.");
        }

        Status = ImportBatchStatus.Cancelled;
    }
}
