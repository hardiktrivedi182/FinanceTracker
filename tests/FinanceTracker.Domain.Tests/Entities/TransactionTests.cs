using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceTracker.Domain.Tests.Entities;

public class TransactionTests
{
    private readonly Guid _id = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly DateOnly _date = new(2026, 10, 15);
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Constructor_DebitTransaction_SetsSignedAmountNegative()
    {
        var tx = new Transaction(
            _id,
            _householdId,
            _accountId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            12,
            _date,
            null,
            "Grocery Store",
            45.50m,
            TransactionType.Debit,
            "USD",
            "fake-fingerprint",
            "user-1",
            _now);

        tx.Amount.Should().Be(45.50m);
        tx.Type.Should().Be(TransactionType.Debit);
        tx.SignedAmount.Should().Be(-45.50m);
        tx.CurrencyCode.Should().Be("USD");
        tx.OriginalTransactionDate.Should().Be(_date);
        tx.SourceRowNumber.Should().Be(12);
    }

    [Fact]
    public void Constructor_CreditTransaction_SetsSignedAmountPositive()
    {
        var tx = new Transaction(
            _id,
            _householdId,
            _accountId,
            null,
            null,
            null,
            _date,
            _date.AddDays(-1),
            "Payroll Deposit",
            2500.00m,
            TransactionType.Credit,
            "EUR",
            "fake-fingerprint",
            "user-1",
            _now);

        tx.Amount.Should().Be(2500.00m);
        tx.Type.Should().Be(TransactionType.Credit);
        tx.SignedAmount.Should().Be(2500.00m);
        tx.CurrencyCode.Should().Be("EUR");
        tx.OriginalTransactionDate.Should().Be(_date.AddDays(-1));
    }

    [Fact]
    public void Constructor_NegativeAmount_ThrowsArgumentException()
    {
        var act = () => new Transaction(
            _id,
            _householdId,
            _accountId,
            null,
            null,
            null,
            _date,
            null,
            "Invalid",
            -10m,
            TransactionType.Debit,
            "USD",
            "fingerprint",
            "user-1",
            _now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*non-negative*");
    }

    [Fact]
    public void Constructor_EmptyDescriptionOrCurrency_ThrowsArgumentException()
    {
        var actDesc = () => new Transaction(
            _id,
            _householdId,
            _accountId,
            null,
            null,
            null,
            _date,
            null,
            "   ",
            10m,
            TransactionType.Debit,
            "USD",
            "fingerprint",
            "user-1",
            _now);

        actDesc.Should().Throw<ArgumentException>();

        var actCurr = () => new Transaction(
            _id,
            _householdId,
            _accountId,
            null,
            null,
            null,
            _date,
            null,
            "Valid",
            10m,
            TransactionType.Debit,
            "INVALID_CURRENCY",
            "fingerprint",
            "user-1",
            _now);

        actCurr.Should().Throw<ArgumentException>()
            .WithMessage("*3-letter ISO*");
    }

    [Fact]
    public void AsMoney_ReturnsConsistentMoneyValueObject()
    {
        var tx = new Transaction(
            _id,
            _householdId,
            _accountId,
            null,
            null,
            null,
            _date,
            null,
            "Coffee",
            4.25m,
            TransactionType.Debit,
            "USD",
            "fingerprint",
            "user-1",
            _now);

        var money = tx.AsMoney();
        money.Amount.Should().Be(4.25m);
        money.CurrencyCode.Should().Be("USD");
    }
}
