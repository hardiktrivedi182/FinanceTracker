namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Indicates the duplicate detection outcome for a staged transaction row.
/// </summary>
public enum DuplicateStatus
{
    None = 0,
    PossibleDuplicate = 1,
    ExactDuplicate = 2
}
