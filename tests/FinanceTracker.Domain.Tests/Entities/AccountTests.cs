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

    [Fact]
    public void AddValuation_ValidValuation_AddsToValuationsCollection()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "PPF Account",
            AccountType.Ppf,
            OwnershipType.Individual,
            "user-1",
            "INR",
            "user-1",
            _now);

        var valId = Guid.NewGuid();
        var valDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var valuation = account.AddValuation(
            valId,
            valDate,
            150000.00m,
            "INR",
            ValuationSourceType.Manual,
            "Passbook 2026",
            "Yearly contribution",
            "user-1",
            _now.AddMinutes(5));

        account.Valuations.Should().ContainSingle();
        valuation.Amount.Should().Be(150000.00m);
        valuation.CurrencyCode.Should().Be("INR");
        valuation.AsMoney().Amount.Should().Be(150000.00m);
        account.LatestValuation.Should().Be(valuation);
        account.UpdatedAt.Should().Be(_now.AddMinutes(5));
    }

    [Fact]
    public void AddValuation_CurrencyMismatch_ThrowsInvalidOperationException()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "EPF Account",
            AccountType.Epf,
            OwnershipType.Individual,
            "user-1",
            "INR",
            "user-1",
            _now);

        var act = () => account.AddValuation(
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            500000m,
            "USD",
            ValuationSourceType.Manual,
            null,
            null,
            "user-1",
            _now);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must match account currency*");
    }

    [Fact]
    public void LatestValuation_ReturnsMostRecentValuationByDate()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "NPS Tier 1",
            AccountType.Nps,
            OwnershipType.Individual,
            "user-1",
            "INR",
            "user-1",
            _now);

        account.AddValuation(
            Guid.NewGuid(),
            new DateOnly(2026, 1, 15),
            100000m,
            "INR",
            ValuationSourceType.Manual,
            null,
            null,
            "user-1",
            _now);

        var newerValuation = account.AddValuation(
            Guid.NewGuid(),
            new DateOnly(2026, 3, 31),
            120000m,
            "INR",
            ValuationSourceType.StatementImport,
            null,
            null,
            "user-1",
            _now.AddDays(1));

        account.LatestValuation.Should().Be(newerValuation);
        account.LatestValuation!.Amount.Should().Be(120000m);
    }

    [Fact]
    public void GetValuationAsOf_ReturnsLatestValuationOnOrBeforeDate()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "PPF Account",
            AccountType.Ppf,
            OwnershipType.Individual,
            "user-1",
            "INR",
            "user-1",
            _now);

        var valJan = account.AddValuation(
            Guid.NewGuid(),
            new DateOnly(2026, 1, 31),
            100000m,
            "INR",
            ValuationSourceType.Manual,
            null,
            null,
            "user-1",
            _now);

        account.AddValuation(
            Guid.NewGuid(),
            new DateOnly(2026, 3, 31),
            150000m,
            "INR",
            ValuationSourceType.Manual,
            null,
            null,
            "user-1",
            _now.AddDays(1));

        // As of Feb 15, should return January valuation
        var asOfFeb = account.GetValuationAsOf(new DateOnly(2026, 2, 15));
        asOfFeb.Should().NotBeNull();
        asOfFeb!.Id.Should().Be(valJan.Id);
        asOfFeb.Amount.Should().Be(100000m);

        // As of Jan 1, should return null (prior to any valuation)
        account.GetValuationAsOf(new DateOnly(2026, 1, 1)).Should().BeNull();
    }

    [Fact]
    public void GetValuationAsOf_WhenMultipleValuationsOnSameDate_ReturnsMostRecentlyCreated()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "Stock Account",
            AccountType.Investment,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        var sameDate = new DateOnly(2026, 6, 30);
        account.AddValuation(
            Guid.NewGuid(),
            sameDate,
            50000m,
            "USD",
            ValuationSourceType.Manual,
            null,
            "Morning snapshot",
            "user-1",
            _now);

        var afternoonValuation = account.AddValuation(
            Guid.NewGuid(),
            sameDate,
            52000m,
            "USD",
            ValuationSourceType.Manual,
            null,
            "Afternoon corrected snapshot",
            "user-1",
            _now.AddHours(4));

        var result = account.GetValuationAsOf(sameDate);
        result.Should().Be(afternoonValuation);
        result!.Amount.Should().Be(52000m);
    }

    [Fact]
    public void UpdateValuation_UpdatesValuationFieldsAndSetsAuditTrail()
    {
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "Emergency Fund",
            AccountType.Savings,
            OwnershipType.Joint,
            null,
            "USD",
            "user-1",
            _now);

        var valId = Guid.NewGuid();
        account.AddValuation(
            valId,
            new DateOnly(2026, 5, 1),
            10000m,
            "USD",
            ValuationSourceType.Manual,
            null,
            "Original notes",
            "user-1",
            _now);

        var modifiedAt = _now.AddDays(2);
        var updated = account.UpdateValuation(
            valId,
            12000m,
            new DateOnly(2026, 5, 2),
            ValuationSourceType.Reconciled,
            "BankRef-999",
            "Corrected bank balance after reconciliation",
            "user-admin",
            modifiedAt);

        updated.Amount.Should().Be(12000m);
        updated.ValuationDate.Should().Be(new DateOnly(2026, 5, 2));
        updated.SourceType.Should().Be(ValuationSourceType.Reconciled);
        updated.SourceReference.Should().Be("BankRef-999");
        updated.Notes.Should().Be("Corrected bank balance after reconciliation");
        updated.CreatedByUserId.Should().Be("user-1");
        updated.CreatedAt.Should().Be(_now);
        updated.UpdatedByUserId.Should().Be("user-admin");
        updated.UpdatedAt.Should().Be(modifiedAt);
        account.UpdatedAt.Should().Be(modifiedAt);
    }

    [Fact]
    public void AccountTypeExtensions_CorrectlyIdentifiesAssetsAndLiabilities()
    {
        AccountType.Checking.IsAsset().Should().BeTrue();
        AccountType.Savings.IsAsset().Should().BeTrue();
        AccountType.FixedDeposit.IsAsset().Should().BeTrue();
        AccountType.Investment.IsAsset().Should().BeTrue();
        AccountType.Epf.IsAsset().Should().BeTrue();
        AccountType.Ppf.IsAsset().Should().BeTrue();
        AccountType.Nps.IsAsset().Should().BeTrue();
        AccountType.Other.IsAsset().Should().BeTrue();

        AccountType.CreditCard.IsLiability().Should().BeTrue();
        AccountType.Loan.IsLiability().Should().BeTrue();
        AccountType.CreditCard.IsAsset().Should().BeFalse();
        AccountType.Loan.IsAsset().Should().BeFalse();
    }
}

