namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an import batch.
/// </summary>
public enum ImportBatchStatus
{
    AwaitingReview = 1,
    ReviewRequired = 2,
    ReadyToCommit = 3,
    Committed = 4,
    Cancelled = 5
}
