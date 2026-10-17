namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Tracks the processing status of a statement import.
/// </summary>
public enum ImportStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
