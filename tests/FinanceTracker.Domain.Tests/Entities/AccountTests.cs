using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceTracker.Domain.Tests.Entities;

public class AccountTests
{
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Constructor_JointAccount_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var account = new Account(
            id,
            _householdId,
            "Household Checking",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        account.Id.Should().Be(id);
        account.HouseholdId.Should().Be(_householdId);
        account.Name.Should().Be("Household Checking");
        account.AccountType.Should().Be(AccountType.Checking);
        account.OwnershipType.Should().Be(OwnershipType.Joint);
        account.OwnerUserId.Should().BeNull();
        account.CurrencyCode.Should().Be("USD");
        account.CreatedByUserId.Should().Be("user-1");
        account.CreatedAt.Should().Be(_now);
        account.UpdatedAt.Should().Be(_now);
        account.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_IndividualAccount_SetsOwnerUserId()
    {
        var id = Guid.NewGuid();
        var account = new Account(
            id,
            _householdId,
            "Sam's Credit Card",
            AccountType.CreditCard,
            OwnershipType.Individual,
            "user-sam",
            "usd",
            "user-sam",
            _now);

        account.OwnershipType.Should().Be(OwnershipType.Individual);
        account.OwnerUserId.Should().Be("user-sam");
        account.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public void Constructor_IndividualAccount_WithoutOwnerUserId_ThrowsArgumentException()
    {
        var act = () => new Account(
            Guid.NewGuid(),
            _householdId,
            "Personal Savings",
            AccountType.Savings,
            OwnershipType.Individual,
            null,
            "USD",
            "user-1",
            _now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*OwnerUserId is required when OwnershipType is Individual*");
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException()
    {
        var act = () => new Account(
            Guid.Empty,
            _householdId,
            "Checking",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Account Id cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyHouseholdId_ThrowsArgumentException()
    {
        var act = () => new Account(
            Guid.NewGuid(),
            Guid.Empty,
            "Checking",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Household Id cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? name)
    {
        var act = () => new Account(
            Guid.NewGuid(),
            _householdId,
            name!,
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalseAndUpdatesTimestamp()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "Checking",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        var later = _now.AddHours(1);
        account.Deactivate(later);

        account.IsActive.Should().BeFalse();
        account.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Activate_SetsIsActiveToTrueAndUpdatesTimestamp()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "Checking",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        account.Deactivate(_now);
        var later = _now.AddHours(2);
        account.Activate(later);

        account.IsActive.Should().BeTrue();
        account.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Update_ModifiesPropertiesAndUpdatesTimestamp()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "Checking",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        var later = _now.AddDays(1);
        account.Update("Renamed Checking", AccountType.Savings, OwnershipType.Individual, "user-2", later);

        account.Name.Should().Be("Renamed Checking");
        account.AccountType.Should().Be(AccountType.Savings);
        account.OwnershipType.Should().Be(OwnershipType.Individual);
        account.OwnerUserId.Should().Be("user-2");
        account.UpdatedAt.Should().Be(later);
    }
}
