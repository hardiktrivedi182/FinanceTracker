using FinanceTracker.Application.Accounts.Dtos;
using FinanceTracker.Application.Accounts.Persistence;
using FinanceTracker.Application.Accounts.Services;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinanceTracker.Application.Tests.Accounts;

public class AccountServiceTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly IHouseholdContext _householdContext = Substitute.For<IHouseholdContext>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly string _userId = "user-partner-1";

    public AccountServiceTests()
    {
        _householdContext.HouseholdId.Returns(_householdId);
        _householdContext.HasHousehold.Returns(true);
        _householdContext.Role.Returns(HouseholdRole.Member);

        _currentUser.UserId.Returns(_userId);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Email.Returns("partner1@example.com");
    }

    [Fact]
    public async Task GetAccountsAsync_ReturnsAccountsMappedToDtos()
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var account = new Account(
            Guid.NewGuid(),
            _householdId,
            "Joint Savings",
            AccountType.Savings,
            OwnershipType.Joint,
            null,
            "USD",
            _userId,
            now);

        account.AddValuation(
            Guid.NewGuid(),
            today,
            5000m,
            "USD",
            ValuationSourceType.Manual,
            null,
            null,
            _userId,
            now);

        _repository.GetByHouseholdIdAsync(_householdId, false, Arg.Any<CancellationToken>())
            .Returns(new List<Account> { account });

        var sut = new AccountService(_repository, _householdContext, _currentUser);

        var result = await sut.GetAccountsAsync();

        result.Should().ContainSingle();
        var dto = result[0];
        dto.Name.Should().Be("Joint Savings");
        dto.AccountType.Should().Be(AccountType.Savings);
        dto.OwnershipType.Should().Be(OwnershipType.Joint);
        dto.LatestBalance.Should().Be(5000m);
        dto.IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAccountAsync_JointAccount_SetsCorrectOwnershipAndAddsToRepository()
    {
        var request = new CreateAccountRequest(
            "Main Checking",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD");

        var sut = new AccountService(_repository, _householdContext, _currentUser);

        var result = await sut.CreateAccountAsync(request);

        result.Name.Should().Be("Main Checking");
        result.OwnershipType.Should().Be(OwnershipType.Joint);
        result.OwnerUserId.Should().BeNull();
        result.CreatedByUserId.Should().Be(_userId);

        await _repository.Received(1).AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAccountAsync_WithInitialBalance_AddsValuation()
    {
        var request = new CreateAccountRequest(
            "EPF Retirement",
            AccountType.Epf,
            OwnershipType.Individual,
            _userId,
            "INR",
            InitialBalance: 1250000m);

        var sut = new AccountService(_repository, _householdContext, _currentUser);

        var result = await sut.CreateAccountAsync(request);

        result.Name.Should().Be("EPF Retirement");
        result.AccountType.Should().Be(AccountType.Epf);
        result.OwnershipType.Should().Be(OwnershipType.Individual);
        result.OwnerUserId.Should().Be(_userId);
        result.LatestBalance.Should().Be(1250000m);
    }

    [Fact]
    public async Task UpdateAccountAsync_NonOwnerModifyingIndividualAccount_ThrowsUnauthorized()
    {
        var accountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var existingAccount = new Account(
            accountId,
            _householdId,
            "Partner 2 Personal",
            AccountType.Savings,
            OwnershipType.Individual,
            "user-partner-2", // Owned by partner 2
            "USD",
            "user-partner-2",
            now);

        _repository.GetByIdAsync(_householdId, accountId, Arg.Any<CancellationToken>())
            .Returns(existingAccount);

        var request = new UpdateAccountRequest(
            "Renamed Account",
            AccountType.Savings,
            OwnershipType.Individual,
            "user-partner-2");

        var sut = new AccountService(_repository, _householdContext, _currentUser);

        var act = () => sut.UpdateAccountAsync(accountId, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Only the account owner or a household admin may modify this individual account*");
    }

    [Fact]
    public async Task UpdateAccountAsync_OwnerModifyingIndividualAccount_Succeeds()
    {
        var accountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var existingAccount = new Account(
            accountId,
            _householdId,
            "Partner 1 NPS",
            AccountType.Nps,
            OwnershipType.Individual,
            _userId, // Owned by caller
            "INR",
            _userId,
            now);

        _repository.GetByIdAsync(_householdId, accountId, Arg.Any<CancellationToken>())
            .Returns(existingAccount);

        var request = new UpdateAccountRequest(
            "Updated NPS Scheme E",
            AccountType.Nps,
            OwnershipType.Individual,
            _userId);

        var sut = new AccountService(_repository, _householdContext, _currentUser);

        var result = await sut.UpdateAccountAsync(accountId, request);

        result.Name.Should().Be("Updated NPS Scheme E");
        await _repository.Received(1).UpdateAsync(existingAccount, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAccountAsync_NonOwner_ThrowsUnauthorized()
    {
        var accountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var existingAccount = new Account(
            accountId,
            _householdId,
            "Partner 2 Credit Card",
            AccountType.CreditCard,
            OwnershipType.Individual,
            "user-partner-2",
            "USD",
            "user-partner-2",
            now);

        _repository.GetByIdAsync(_householdId, accountId, Arg.Any<CancellationToken>())
            .Returns(existingAccount);

        var sut = new AccountService(_repository, _householdContext, _currentUser);

        var act = () => sut.DeactivateAccountAsync(accountId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RecordValuationAsync_AddsValuationAndUpdatesAccount()
    {
        var accountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var existingAccount = new Account(
            accountId,
            _householdId,
            "Fixed Deposit",
            AccountType.FixedDeposit,
            OwnershipType.Joint,
            null,
            "INR",
            _userId,
            now);

        _repository.GetByIdAsync(_householdId, accountId, Arg.Any<CancellationToken>())
            .Returns(existingAccount);

        var request = new RecordValuationRequest(
            new DateOnly(2026, 4, 1),
            550000m,
            "Quarterly interest credit",
            "FD-12345");

        var sut = new AccountService(_repository, _householdContext, _currentUser);

        var result = await sut.RecordValuationAsync(accountId, request);

        result.Amount.Should().Be(550000m);
        result.CurrencyCode.Should().Be("INR");
        result.Notes.Should().Be("Quarterly interest credit");
        result.SourceReference.Should().Be("FD-12345");
        existingAccount.LatestValuation!.Amount.Should().Be(550000m);
    }
}
