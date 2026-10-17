using FinanceTracker.Application.Accounts.Persistence;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.NetWorth.Services;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinanceTracker.Application.Tests.NetWorth;

public class NetWorthServiceTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly IHouseholdContext _householdContext = Substitute.For<IHouseholdContext>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly DateTimeOffset _baseTime = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public NetWorthServiceTests()
    {
        _householdContext.HouseholdId.Returns(_householdId);
        _householdContext.HasHousehold.Returns(true);
        _householdContext.Role.Returns(HouseholdRole.Admin);

        _currentUser.UserId.Returns("partner-1");
        _currentUser.IsAuthenticated.Returns(true);
    }

    [Fact]
    public async Task GetNetWorthAsync_CalculatesAssetsLiabilitiesAndNetWorth_PerCurrency()
    {
        var accSavings = new Account(Guid.NewGuid(), _householdId, "Emergency Fund", AccountType.Savings, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);
        accSavings.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 500000m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        var accFd = new Account(Guid.NewGuid(), _householdId, "Fixed Deposit", AccountType.FixedDeposit, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);
        accFd.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 1000000m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        var accCredit = new Account(Guid.NewGuid(), _householdId, "Credit Card", AccountType.CreditCard, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);
        accCredit.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 50000m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        var accUsdInvest = new Account(Guid.NewGuid(), _householdId, "US Stocks", AccountType.Investment, OwnershipType.Joint, null, "USD", "partner-1", _baseTime);
        accUsdInvest.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 20000m, "USD", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { accSavings, accFd, accCredit, accUsdInvest });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var report = await sut.GetNetWorthAsync(new DateOnly(2026, 6, 1));

        report.Currencies.Should().HaveCount(2);

        var inr = report.Currencies.First(c => c.CurrencyCode == "INR");
        inr.TotalAssets.Should().Be(1500000m); // 500k + 1000k
        inr.TotalLiabilities.Should().Be(50000m);
        inr.NetWorth.Should().Be(1450000m); // 1500k - 50k

        var usd = report.Currencies.First(c => c.CurrencyCode == "USD");
        usd.TotalAssets.Should().Be(20000m);
        usd.TotalLiabilities.Should().Be(0m);
        usd.NetWorth.Should().Be(20000m);
    }

    [Fact]
    public async Task GetNetWorthAsync_CalculatesOwnershipBreakdownAccurately()
    {
        // Joint account: 200,000 INR
        var accJoint = new Account(Guid.NewGuid(), _householdId, "Joint Account", AccountType.Savings, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);
        accJoint.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 200000m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        // Partner 1 Individual EPF: 800,000 INR
        var accP1 = new Account(Guid.NewGuid(), _householdId, "P1 EPF", AccountType.Epf, OwnershipType.Individual, "partner-1", "INR", "partner-1", _baseTime);
        accP1.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 800000m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        // Partner 2 Individual PPF: 400,000 INR
        var accP2 = new Account(Guid.NewGuid(), _householdId, "P2 PPF", AccountType.Ppf, OwnershipType.Individual, "partner-2", "INR", "partner-2", _baseTime);
        accP2.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 400000m, "INR", ValuationSourceType.Manual, null, null, "partner-2", _baseTime);

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { accJoint, accP1, accP2 });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var report = await sut.GetNetWorthAsync(new DateOnly(2026, 6, 1));
        var inr = report.Currencies.Single();

        inr.NetWorth.Should().Be(1400000m);
        inr.OwnershipBreakdown.JointTotal.Should().Be(200000m);
        inr.OwnershipBreakdown.Partner1IndividualTotal.Should().Be(800000m);
        inr.OwnershipBreakdown.Partner2IndividualTotal.Should().Be(400000m);

        // P1 total share = 800k + (200k / 2) = 900,000
        inr.OwnershipBreakdown.Partner1TotalShare.Should().Be(900000m);
        // P2 total share = 400k + (200k / 2) = 500,000
        inr.OwnershipBreakdown.Partner2TotalShare.Should().Be(500000m);
    }

    [Fact]
    public async Task GetNetWorthAsync_IdentifiesStaleValuation_BasedOnConfiguredThreshold()
    {
        var targetDate = new DateOnly(2026, 6, 1);
        var oldValuationDate = new DateOnly(2026, 1, 1); // 151 days before targetDate

        var account = new Account(Guid.NewGuid(), _householdId, "Old Valuation Bank", AccountType.Checking, OwnershipType.Joint, null, "USD", "partner-1", _baseTime);
        account.AddValuation(Guid.NewGuid(), oldValuationDate, 5000m, "USD", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        // Threshold = 90 days → 151 days should be flagged as stale
        var reportStale = await sut.GetNetWorthAsync(targetDate, stalenessThresholdDays: 90);
        reportStale.Currencies[0].Accounts[0].IsStale.Should().BeTrue();

        // Threshold = 180 days → 151 days should NOT be flagged as stale
        var reportFresh = await sut.GetNetWorthAsync(targetDate, stalenessThresholdDays: 180);
        reportFresh.Currencies[0].Accounts[0].IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task GetNetWorthAsync_FlagsAccountWithMissingValuation_OnAsOfDate()
    {
        var targetDate = new DateOnly(2026, 1, 1);
        var valuationDate = new DateOnly(2026, 2, 1); // valuation is in the future relative to targetDate

        var account = new Account(Guid.NewGuid(), _householdId, "New Account", AccountType.Checking, OwnershipType.Joint, null, "USD", "partner-1", _baseTime);
        account.AddValuation(Guid.NewGuid(), valuationDate, 5000m, "USD", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var report = await sut.GetNetWorthAsync(targetDate);
        var item = report.Currencies[0].Accounts[0];

        item.HasMissingValuation.Should().BeTrue();
        item.Amount.Should().Be(0m);
    }

    [Fact]
    public async Task CompareNetWorthAsync_CalculatesDeltaBetweenTwoDates()
    {
        var date1 = new DateOnly(2026, 1, 1);
        var date2 = new DateOnly(2026, 6, 1);

        var account = new Account(Guid.NewGuid(), _householdId, "Growth Fund", AccountType.Investment, OwnershipType.Joint, null, "USD", "partner-1", _baseTime);
        account.AddValuation(Guid.NewGuid(), date1, 100000m, "USD", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);
        account.AddValuation(Guid.NewGuid(), date2, 120000m, "USD", ValuationSourceType.Manual, null, null, "partner-1", _baseTime.AddDays(150));

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var comparison = await sut.CompareNetWorthAsync(date1, date2);

        comparison.Currencies.Should().ContainSingle();
        var usd = comparison.Currencies[0];
        usd.CurrencyCode.Should().Be("USD");
        usd.Date1NetWorth.Should().Be(100000m);
        usd.Date2NetWorth.Should().Be(120000m);
        usd.AbsoluteChange.Should().Be(20000m);
        usd.PercentageChange.Should().Be(20.00m);
    }

    [Fact]
    public async Task GetNetWorthAsync_WithNoAccounts_ReturnsEmptyCurrencies()
    {
        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account>());

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var report = await sut.GetNetWorthAsync(new DateOnly(2026, 6, 1));

        report.Currencies.Should().BeEmpty();
        report.AsOfDate.Should().Be(new DateOnly(2026, 6, 1));
    }

    [Fact]
    public async Task GetNetWorthAsync_WithArbitraryUserIds_CalculatesIndividualBreakdownAccurately()
    {
        // Users are arbitrary IDs, e.g. GUIDs from Identity
        const string userAlice = "user-alice-guid-123";
        const string userBob = "user-bob-guid-456";

        var joint = new Account(Guid.NewGuid(), _householdId, "Joint Savings", AccountType.Savings, OwnershipType.Joint, null, "INR", userAlice, _baseTime);
        joint.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 100000m, "INR", ValuationSourceType.Manual, null, null, userAlice, _baseTime);

        var alicePpf = new Account(Guid.NewGuid(), _householdId, "Alice PPF", AccountType.Ppf, OwnershipType.Individual, userAlice, "INR", userAlice, _baseTime);
        alicePpf.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 300000m, "INR", ValuationSourceType.Manual, null, null, userAlice, _baseTime);

        var bobEpf = new Account(Guid.NewGuid(), _householdId, "Bob EPF", AccountType.Epf, OwnershipType.Individual, userBob, "INR", userBob, _baseTime);
        bobEpf.AddValuation(Guid.NewGuid(), new DateOnly(2026, 5, 1), 500000m, "INR", ValuationSourceType.Manual, null, null, userBob, _baseTime);

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { joint, alicePpf, bobEpf });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var report = await sut.GetNetWorthAsync(new DateOnly(2026, 6, 1));
        var inr = report.Currencies.Single();

        inr.NetWorth.Should().Be(900000m);
        inr.OwnershipBreakdown.JointTotal.Should().Be(100000m);
        // Alice individual total = 300k, Bob individual total = 500k
        var indTotals = new[] { inr.OwnershipBreakdown.Partner1IndividualTotal, inr.OwnershipBreakdown.Partner2IndividualTotal };
        indTotals.Should().Contain(300000m);
        indTotals.Should().Contain(500000m);

        // Total shares must include 50% of joint total (50,000 each)
        var shares = new[] { inr.OwnershipBreakdown.Partner1TotalShare, inr.OwnershipBreakdown.Partner2TotalShare };
        shares.Should().Contain(350000m);
        shares.Should().Contain(550000m);
    }

    [Theory]
    [InlineData(89, false)] // 89 days -> fresh
    [InlineData(90, false)] // 90 days -> boundary condition: not strictly greater than 90 -> fresh
    [InlineData(91, true)]  // 91 days -> strictly greater than 90 -> stale
    public async Task GetNetWorthAsync_Exact90DayStalenessThreshold_RespectsStrictBoundary(int daysOffset, bool expectedStale)
    {
        var targetDate = new DateOnly(2026, 6, 1);
        var valDate = targetDate.AddDays(-daysOffset);

        var account = new Account(Guid.NewGuid(), _householdId, "Checking", AccountType.Checking, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);
        account.AddValuation(Guid.NewGuid(), valDate, 10000m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var report = await sut.GetNetWorthAsync(targetDate, stalenessThresholdDays: 90);
        report.Currencies[0].Accounts[0].IsStale.Should().Be(expectedStale);
    }

    [Fact]
    public async Task GetNetWorthAsync_WithNegativeBalancesAndPrecision_CalculatesAccurately()
    {
        var targetDate = new DateOnly(2026, 6, 1);

        // Overdrawn checking account with negative balance
        var accOverdrawn = new Account(Guid.NewGuid(), _householdId, "Overdrawn Account", AccountType.Checking, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);
        accOverdrawn.AddValuation(Guid.NewGuid(), targetDate, -5432.1234m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        // Credit card liability
        var accCard = new Account(Guid.NewGuid(), _householdId, "Credit Card", AccountType.CreditCard, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);
        accCard.AddValuation(Guid.NewGuid(), targetDate, 15000.5678m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        // Savings asset
        var accSavings = new Account(Guid.NewGuid(), _householdId, "Savings Account", AccountType.Savings, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);
        accSavings.AddValuation(Guid.NewGuid(), targetDate, 100000.0000m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { accOverdrawn, accCard, accSavings });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var report = await sut.GetNetWorthAsync(targetDate);
        var inr = report.Currencies.Single();

        // Assets = 100000 + (-5432.1234) = 94567.8766m
        inr.TotalAssets.Should().Be(94567.8766m);
        // Liabilities = 15000.5678m
        inr.TotalLiabilities.Should().Be(15000.5678m);
        // Net worth = 94567.8766 - 15000.5678 = 79567.3088m
        inr.NetWorth.Should().Be(79567.3088m);
    }

    [Fact]
    public async Task GetNetWorthAsync_MultipleValuationsOnSameDate_ResolvesMostRecentCreation()
    {
        var targetDate = new DateOnly(2026, 6, 1);
        var account = new Account(Guid.NewGuid(), _householdId, "Gold ETF", AccountType.Investment, OwnershipType.Joint, null, "INR", "partner-1", _baseTime);

        // Morning valuation: 50,000
        account.AddValuation(Guid.NewGuid(), targetDate, 50000m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime);
        // Afternoon valuation: 52,500
        account.AddValuation(Guid.NewGuid(), targetDate, 52500m, "INR", ValuationSourceType.Manual, null, null, "partner-1", _baseTime.AddHours(5));

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account });

        var sut = new NetWorthService(_repository, _householdContext, _currentUser);

        var report = await sut.GetNetWorthAsync(targetDate);
        var item = report.Currencies[0].Accounts[0];

        item.Amount.Should().Be(52500m);
    }
}

