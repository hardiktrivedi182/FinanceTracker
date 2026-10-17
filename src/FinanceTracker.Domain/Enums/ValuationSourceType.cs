namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Identifies the provenance of an account or holding valuation.
/// </summary>
public enum ValuationSourceType
{
    Manual = 0,
    StatementImport = 1,
    Reconciled = 2
}
