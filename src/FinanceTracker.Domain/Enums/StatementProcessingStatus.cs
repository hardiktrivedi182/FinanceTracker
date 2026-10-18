namespace FinanceTracker.Domain.Enums;

public enum StatementProcessingStatus
{
    Uploaded = 1,
    Parsing = 2,
    Parsed = 3,
    Failed = 4,
    Committed = 5
}
