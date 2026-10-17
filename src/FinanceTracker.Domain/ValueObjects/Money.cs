namespace FinanceTracker.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with an explicit currency.
/// Uses decimal arithmetic to avoid floating-point precision errors.
/// Maximum supported scale matches SQL decimal(19,4) precision.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    private const string DefaultCurrencyCode = "XXX";
    private const int MonetaryScale = 4;
    private readonly string? _currencyCode;

    /// <summary>
    /// The monetary amount. Always stored as decimal for financial precision.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// ISO 4217 currency code (e.g., "USD", "EUR", "GBP").
    /// Defaults to "XXX" (ISO 4217 for 'No currency') when uninitialized.
    /// </summary>
    public string CurrencyCode => _currencyCode ?? DefaultCurrencyCode;

    /// <summary>
    /// Initializes a default Money instance with 0 amount and "XXX" currency.
    /// </summary>
    public Money()
    {
        Amount = 0m;
        _currencyCode = DefaultCurrencyCode;
    }

    public Money(decimal amount, string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        string trimmed = currencyCode.Trim();
        if (trimmed.Length != 3 || !trimmed.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "Currency code must be a 3-letter ISO 4217 ASCII code.",
                nameof(currencyCode));
        }

        Amount = amount;
        _currencyCode = trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// Rounds the amount using banker's rounding (MidpointRounding.ToEven)
    /// to the specified number of decimal places (between 0 and 4).
    /// </summary>
    public Money Round(int decimals = 2)
    {
        if (decimals is < 0 or > MonetaryScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decimals),
                $"Decimal places must be between 0 and {MonetaryScale}.");
        }

        return new Money(Math.Round(Amount, decimals, MidpointRounding.ToEven), CurrencyCode);
    }

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

    public static Money operator *(Money money, decimal factor)
    {
        decimal product = Math.Round(money.Amount * factor, MonetaryScale, MidpointRounding.ToEven);
        return new Money(product, money.CurrencyCode);
    }

    public static Money operator *(decimal factor, Money money) =>
        money * factor;

    /// <summary>
    /// Divides money by a divisor and rounds using banker's rounding to 4 decimal places.
    /// </summary>
    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0m)
        {
            throw new DivideByZeroException("Cannot divide Money by zero.");
        }

        decimal quotient = Math.Round(money.Amount / divisor, MonetaryScale, MidpointRounding.ToEven);
        return new Money(quotient, money.CurrencyCode);
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

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
                $"Cannot perform operation on different currencies: {left.CurrencyCode} and {right.CurrencyCode}.");
        }
    }
}
