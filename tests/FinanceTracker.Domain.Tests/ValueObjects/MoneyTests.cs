using FluentAssertions;
using FinanceTracker.Domain.ValueObjects;
using Xunit;

namespace FinanceTracker.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var money = new Money(100.50m, "USD");

        money.Amount.Should().Be(100.50m);
        money.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public void Constructor_NormalizesCurrencyToUpperCase()
    {
        var money = new Money(10m, "usd");

        money.CurrencyCode.Should().Be("USD");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidCurrency_ThrowsArgumentException(string? currency)
    {
        var act = () => new Money(10m, currency!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDX")]
    public void Constructor_WithNonThreeLetterCurrency_ThrowsArgumentException(string currency)
    {
        var act = () => new Money(10m, currency);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*3-letter ISO 4217*");
    }

    [Fact]
    public void Addition_SameCurrency_ReturnsSum()
    {
        var a = new Money(10.25m, "USD");
        var b = new Money(5.75m, "USD");

        var result = a + b;

        result.Amount.Should().Be(16.00m);
        result.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public void Addition_DifferentCurrency_ThrowsInvalidOperation()
    {
        var usd = new Money(10m, "USD");
        var eur = new Money(10m, "EUR");

        var act = () => usd + eur;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtraction_SameCurrency_ReturnsDifference()
    {
        var a = new Money(20.00m, "USD");
        var b = new Money(7.50m, "USD");

        var result = a - b;

        result.Amount.Should().Be(12.50m);
    }

    [Fact]
    public void Negation_ReturnsNegativeAmount()
    {
        var money = new Money(42.00m, "USD");

        var result = -money;

        result.Amount.Should().Be(-42.00m);
        result.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public void Multiplication_ByDecimalFactor_ReturnsProduct()
    {
        var money = new Money(100m, "USD");

        var result = money * 0.15m;

        result.Amount.Should().Be(15.0m);
    }

    [Fact]
    public void Round_UsesBankersRounding()
    {
        // Banker's rounding: 2.5 rounds to 2, 3.5 rounds to 4
        var money1 = new Money(2.5m, "USD");
        var money2 = new Money(3.5m, "USD");

        money1.Round(0).Amount.Should().Be(2m);
        money2.Round(0).Amount.Should().Be(4m);
    }

    [Fact]
    public void Round_DefaultTwoDecimalPlaces()
    {
        var money = new Money(10.1234m, "USD");

        var result = money.Round();

        result.Amount.Should().Be(10.12m);
    }

    [Fact]
    public void Zero_ReturnsZeroAmountWithCurrency()
    {
        var zero = Money.Zero("EUR");

        zero.Amount.Should().Be(0m);
        zero.CurrencyCode.Should().Be("EUR");
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_AreEqual()
    {
        var a = new Money(42.50m, "USD");
        var b = new Money(42.50m, "USD");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentAmount_AreNotEqual()
    {
        var a = new Money(42.50m, "USD");
        var b = new Money(42.51m, "USD");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentCurrency_AreNotEqual()
    {
        var a = new Money(42.50m, "USD");
        var b = new Money(42.50m, "EUR");

        a.Should().NotBe(b);
    }

    [Fact]
    public void ToString_FormatsWithCurrency()
    {
        var money = new Money(1234.56m, "USD");

        money.ToString().Should().Contain("USD");
        money.ToString().Should().Contain("1,234.56");
    }

    [Fact]
    public void DecimalPrecision_HandlesSmallAmounts()
    {
        var a = new Money(0.1m, "USD");
        var b = new Money(0.2m, "USD");

        var result = a + b;

        // This is where float would fail (0.1 + 0.2 != 0.3 in floating point)
        result.Amount.Should().Be(0.3m);
    }

    [Fact]
    public void DecimalPrecision_HandlesLargeAmounts()
    {
        var money = new Money(999_999_999_999_999.9999m, "USD");

        money.Amount.Should().Be(999_999_999_999_999.9999m);
    }
}
