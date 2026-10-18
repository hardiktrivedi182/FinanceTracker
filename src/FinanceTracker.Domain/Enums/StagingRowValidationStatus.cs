namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Indicates the deterministic validation state of a staged transaction row.
/// </summary>
public enum StagingRowValidationStatus
{
    Valid = 1,
    Warning = 2,
    Invalid = 3
}
