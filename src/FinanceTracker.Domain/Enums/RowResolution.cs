namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Tracks user reconciliation decisions for an ambiguous, flagged, or excluded candidate row.
/// </summary>
public enum RowResolution
{
    None = 0,
    ApprovedAsIs = 1,
    ManuallyCorrected = 2,
    Excluded = 3
}
