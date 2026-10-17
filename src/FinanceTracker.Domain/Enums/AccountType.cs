namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Classifies the type of financial account.
/// </summary>
public enum AccountType
{
    Checking = 0,
    Savings = 1,
    CreditCard = 2,
    Investment = 3,
    Loan = 4,
    Other = 99
}
