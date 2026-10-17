namespace FinanceTracker.Domain.Enums;

/// <summary>
/// Classifies the type of financial account or asset/liability holding.
/// Supports standard liquid banking, investments, loans, and household wealth instruments (EPF, PPF, NPS, FDs).
/// </summary>
public enum AccountType
{
    Checking = 0,
    Savings = 1,
    CreditCard = 2,
    Investment = 3,
    Loan = 4,
    FixedDeposit = 5,
    Epf = 6,
    Ppf = 7,
    Nps = 8,
    Other = 99
}

public static class AccountTypeExtensions
{
    public static bool IsLiability(this AccountType type) =>
        type is AccountType.CreditCard or AccountType.Loan;

    public static bool IsAsset(this AccountType type) =>
        !type.IsLiability();
}
