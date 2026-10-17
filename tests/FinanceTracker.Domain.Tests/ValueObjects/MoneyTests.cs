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

    [Fact]
    public void Constructor_TrimsWhitespaceFromCurrency()
    {
        var money = new Money(10m, " eur ");

        money.CurrencyCode.Should().Be("EUR");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithInvalidCurrency_ThrowsArgumentException(string? currency)
    {
        var act = () => new Money(10m, currency!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDX")]
    [InlineData("U")]
    [InlineData("123")]
    [InlineData("US1")]
    [InlineData("US$")]
    [InlineData("€UR")]
    public void Constructor_WithNonThreeAsciiLetterCurrency_ThrowsArgumentException(string currency)
    {
        var act = () => new Money(10m, currency);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*3-letter ISO 4217*");
    }

    [Fact]
    public void DefaultStruct_HasZeroAmountAndDefaultCurrency()
    {
        var defaultMoney = default(Money);

        defaultMoney.Amount.Should().Be(0m);
        defaultMoney.CurrencyCode.Should().Be("XXX");
        defaultMoney.ToString().Should().Be("0.00 XXX");
    }

    [Fact]
    public void ParameterlessConstructor_CreatesDefaultMoney()
    {
        var money = new Money();

        money.Amount.Should().Be(0m);
        money.CurrencyCode.Should().Be("XXX");
    }

    [Fact]
    public void DefaultStruct_ArithmeticWithSameCurrency_Works()
    {
        var defaultMoney = default(Money);
        var xxxMoney = new Money(5m, "XXX");

        var sum = defaultMoney + xxxMoney;

        sum.Amount.Should().Be(5m);
        sum.CurrencyCode.Should().Be("XXX");
    }

    [Fact]
    public void DefaultStruct_ArithmeticWithDifferentCurrency_Throws()
    {
        var defaultMoney = default(Money);
        var usd = new Money(10m, "USD");

        var act = () => defaultMoney + usd;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies: XXX and USD*");
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
    public void NegativeAmounts_AllowedForBalancesAndDebits()
    {
        var negative = new Money(-150.25m, "USD");

        negative.Amount.Should().Be(-150.25m);
        negative.ToString().Should().Be("-150.25 USD");
    }

    [Fact]
    public void Multiplication_ByDecimalFactor_RoundsToFourDecimals()
    {
        var money = new Money(100m, "USD");

        var result = money * 0.15555m;

        // 100 * 0.15555 = 15.5550
        result.Amount.Should().Be(15.555m);
        result.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public void Multiplication_FactorOnLeft_ReturnsProduct()
    {
        var money = new Money(50m, "USD");

        var result = 2.5m * money;

        result.Amount.Should().Be(125.0m);
    }

    [Fact]
    public void Division_ByNonZero_RoundsToFourDecimalsBankersRounding()
    {
        var money = new Money(10m, "USD");

        var result = money / 3m;

        // 10 / 3 = 3.333333... rounded to 4 decimals = 3.3333
        result.Amount.Should().Be(3.3333m);
        result.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public void Division_BankersRoundingContract()
    {
        // 0.00025 / 1 = 0.00025 -> rounds to even 0.0002 at 4th decimal
        var m1 = new Money(0.00025m, "USD") / 1m;
        m1.Amount.Should().Be(0.0002m);

        // 0.00035 / 1 = 0.00035 -> rounds to even 0.0004 at 4th decimal
        var m2 = new Money(0.00035m, "USD") / 1m;
        m2.Amount.Should().Be(0.0004m);
    }

    [Fact]
    public void Division_ByZero_ThrowsDivideByZeroException()
    {
        var money = new Money(100m, "USD");

        var act = () => money / 0m;

        act.Should().Throw<DivideByZeroException>()
            .WithMessage("*Cannot divide Money by zero*");
    }

    [Fact]
    public void Comparison_LessThan_SameCurrency_ReturnsExpected()
    {
        var lesser = new Money(10m, "USD");
        var equalToLesser = new Money(10m, "USD");
        var greater = new Money(20m, "USD");

        (lesser < greater).Should().BeTrue();
        (greater < lesser).Should().BeFalse();
        (lesser <= equalToLesser).Should().BeTrue();
        (lesser <= greater).Should().BeTrue();
    }

    [Fact]
    public void Comparison_GreaterThan_SameCurrency_ReturnsExpected()
    {
        var lesser = new Money(10m, "USD");
        var greater = new Money(20m, "USD");
        var equalToGreater = new Money(20m, "USD");

        (greater > lesser).Should().BeTrue();
        (lesser > greater).Should().BeFalse();
        (greater >= equalToGreater).Should().BeTrue();
        (greater >= lesser).Should().BeTrue();
    }

    [Fact]
    public void Comparison_DifferentCurrency_ThrowsInvalidOperationException()
    {
        var usd = new Money(10m, "USD");
        var eur = new Money(10m, "EUR");

        var act1 = () => usd < eur;
        var act2 = () => usd > eur;
        var act3 = () => usd.CompareTo(eur);

        act1.Should().Throw<InvalidOperationException>().WithMessage("*different currencies*");
        act2.Should().Throw<InvalidOperationException>().WithMessage("*different currencies*");
        act3.Should().Throw<InvalidOperationException>().WithMessage("*different currencies*");
    }

    [Fact]
    public void Round_UsesBankersRounding()
    {
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

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Round_InvalidDecimals_ThrowsArgumentOutOfRangeException(int decimals)
    {
        var money = new Money(10m, "USD");

        var act = () => money.Round(decimals);

        act.Should().Throw<ArgumentOutOfRangeException>();
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

        result.Amount.Should().Be(0.3m);
    }

    [Fact]
    public void DecimalPrecision_HandlesLargeAmounts()
    {
        var money = new Money(999_999_999_999_999.9999m, "USD");

        money.Amount.Should().Be(999_999_999_999_999.9999m);
    }

    [Fact]
    public void Overflow_AdditionExceedingDecimalMax_ThrowsOverflowException()
    {
        var max = new Money(decimal.MaxValue, "USD");
        var one = new Money(1m, "USD");

        var act = () => max + one;

        act.Should().Throw<OverflowException>();
    }
}
