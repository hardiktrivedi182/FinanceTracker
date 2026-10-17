namespace FinanceTracker.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with an explicit currency.
/// Uses decimal arithmetic to avoid floating-point precision errors.
/// </summary>
public readonly record struct Money
{
    /// <summary>
    /// The monetary amount. Always stored as decimal for financial precision.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// ISO 4217 currency code (e.g., "USD", "EUR", "GBP").
    /// </summary>
    public string CurrencyCode { get; }

    public Money(decimal amount, string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        if (currencyCode.Length != 3)
        {
            throw new ArgumentException("Currency code must be a 3-letter ISO 4217 code.", nameof(currencyCode));
        }

        Amount = amount;
        CurrencyCode = currencyCode.ToUpperInvariant();
    }

    /// <summary>
    /// Rounds the amount using banker's rounding (MidpointRounding.ToEven)
    /// to the specified number of decimal places.
    /// </summary>
    public Money Round(int decimals = 2) =>
        new(Math.Round(Amount, decimals, MidpointRounding.ToEven), CurrencyCode);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.CurrencyCode);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.CurrencyCode);
    }

    public static Money operator -(Money value) =>
        new(-value.Amount, value.CurrencyCode);

    public static Money operator *(Money money, decimal factor) =>
        new(money.Amount * factor, money.CurrencyCode);

    public static Money operator *(decimal factor, Money money) =>
        money * factor;

    /// <summary>
    /// Creates a zero-value Money in the specified currency.
    /// </summary>
    public static Money Zero(string currencyCode) =>
        new(0m, currencyCode);

    public override string ToString() => $"{Amount:N2} {CurrencyCode}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.CurrencyCode, right.CurrencyCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot perform arithmetic on different currencies: {left.CurrencyCode} and {right.CurrencyCode}.");
        }
    }
}
