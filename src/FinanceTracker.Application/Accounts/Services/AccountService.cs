using FinanceTracker.Application.Accounts.Dtos;
using FinanceTracker.Application.Accounts.Persistence;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IHouseholdContext _householdContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    private const int StalenessThresholdDays = 90;

    public AccountService(
        IAccountRepository repository,
        IHouseholdContext householdContext,
        ICurrentUser currentUser,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _householdContext = householdContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var accounts = await _repository.GetByHouseholdIdAsync(
            _householdContext.HouseholdId,
            includeInactive,
            ct);

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        return accounts.Select(a => MapToDto(a, today)).ToList();
    }

    public async Task<AccountDto?> GetAccountByIdAsync(Guid id, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var account = await _repository.GetByIdAsync(_householdContext.HouseholdId, id, ct);
        if (account == null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        return MapToDto(account, today);
    }

    public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();
        string currentUserId = GetCurrentUserId();

        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        string? ownerUserId = request.OwnershipType == OwnershipType.Individual
            ? (!string.IsNullOrWhiteSpace(request.OwnerUserId) ? request.OwnerUserId : currentUserId)
            : null;

        var account = new Account(
            Guid.NewGuid(),
            _householdContext.HouseholdId,
            request.Name,
            request.AccountType,
            request.OwnershipType,
            ownerUserId,
            request.CurrencyCode,
            currentUserId,
            now);

        await _repository.AddAsync(account, ct);

        if (request.InitialBalance.HasValue)
        {
            var valDate = request.InitialValuationDate ?? today;
            var valuation = account.AddValuation(
                Guid.NewGuid(),
                valDate,
                request.InitialBalance.Value,
                account.CurrencyCode,
                ValuationSourceType.Manual,
                "Initial account opening",
                "Initial balance recorded during account setup",
                currentUserId,
                now);

            await _repository.AddValuationAsync(valuation, ct);
        }

        await _repository.SaveChangesAsync(ct);

        return MapToDto(account, today);
    }

    public async Task<AccountDto> UpdateAccountAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();
        string currentUserId = GetCurrentUserId();

        var account = await _repository.GetByIdAsync(_householdContext.HouseholdId, id, ct)
            ?? throw new KeyNotFoundException($"Account '{id}' was not found in the current household.");

        EnsureAccountWriteAccess(account, currentUserId);

        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        string? ownerUserId = request.OwnershipType == OwnershipType.Individual
            ? (!string.IsNullOrWhiteSpace(request.OwnerUserId) ? request.OwnerUserId : account.OwnerUserId ?? currentUserId)
            : null;

        account.Update(
            request.Name,
            request.AccountType,
            request.OwnershipType,
            ownerUserId,
            now);

        await _repository.UpdateAsync(account, ct);
        await _repository.SaveChangesAsync(ct);

        return MapToDto(account, today);
    }

    public async Task<bool> DeactivateAccountAsync(Guid id, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();
        string currentUserId = GetCurrentUserId();

        var account = await _repository.GetByIdAsync(_householdContext.HouseholdId, id, ct);
        if (account == null)
        {
            return false;
        }

        EnsureAccountWriteAccess(account, currentUserId);

        var now = _timeProvider.GetUtcNow();
        account.Deactivate(now);

        await _repository.UpdateAsync(account, ct);
        await _repository.SaveChangesAsync(ct);

        return true;
    }

    public async Task<AccountValuationDto> RecordValuationAsync(Guid accountId, RecordValuationRequest request, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();
        string currentUserId = GetCurrentUserId();

        var account = await _repository.GetByIdAsync(_householdContext.HouseholdId, accountId, ct)
            ?? throw new KeyNotFoundException($"Account '{accountId}' was not found in the current household.");

        EnsureAccountWriteAccess(account, currentUserId);

        var now = _timeProvider.GetUtcNow();
        var valuation = account.AddValuation(
            Guid.NewGuid(),
            request.ValuationDate,
            request.Amount,
            account.CurrencyCode,
            request.SourceType,
            request.SourceReference,
            request.Notes,
            currentUserId,
            now);

        await _repository.AddValuationAsync(valuation, ct);
        await _repository.UpdateAsync(account, ct);
        await _repository.SaveChangesAsync(ct);

        return new AccountValuationDto(
            valuation.Id,
            valuation.AccountId,
            valuation.ValuationDate,
            valuation.Amount,
            valuation.CurrencyCode,
            valuation.SourceType,
            valuation.SourceReference,
            valuation.Notes,
            valuation.CreatedByUserId,
            valuation.CreatedAt,
            valuation.UpdatedByUserId,
            valuation.UpdatedAt);
    }

    public async Task<AccountValuationDto> UpdateValuationAsync(
        Guid accountId,
        Guid valuationId,
        UpdateValuationRequest request,
        CancellationToken ct = default)
    {
        EnsureHouseholdAccess();
        string currentUserId = GetCurrentUserId();

        var account = await _repository.GetByIdAsync(_householdContext.HouseholdId, accountId, ct)
            ?? throw new KeyNotFoundException($"Account '{accountId}' was not found in the current household.");

        EnsureAccountWriteAccess(account, currentUserId);

        var now = _timeProvider.GetUtcNow();
        var updated = account.UpdateValuation(
            valuationId,
            request.Amount,
            request.ValuationDate,
            request.SourceType,
            request.SourceReference,
            request.Notes,
            currentUserId,
            now);

        await _repository.UpdateAsync(account, ct);
        await _repository.SaveChangesAsync(ct);

        return new AccountValuationDto(
            updated.Id,
            updated.AccountId,
            updated.ValuationDate,
            updated.Amount,
            updated.CurrencyCode,
            updated.SourceType,
            updated.SourceReference,
            updated.Notes,
            updated.CreatedByUserId,
            updated.CreatedAt,
            updated.UpdatedByUserId,
            updated.UpdatedAt);
    }

    public async Task<IReadOnlyList<AccountValuationDto>> GetValuationsAsync(Guid accountId, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var account = await _repository.GetByIdAsync(_householdContext.HouseholdId, accountId, ct)
            ?? throw new KeyNotFoundException($"Account '{accountId}' was not found in the current household.");

        return account.Valuations
            .OrderByDescending(v => v.ValuationDate)
            .ThenByDescending(v => v.CreatedAt)
            .Select(v => new AccountValuationDto(
                v.Id,
                v.AccountId,
                v.ValuationDate,
                v.Amount,
                v.CurrencyCode,
                v.SourceType,
                v.SourceReference,
                v.Notes,
                v.CreatedByUserId,
                v.CreatedAt,
                v.UpdatedByUserId,
                v.UpdatedAt))
            .ToList();
    }

    private void EnsureHouseholdAccess()
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        if (!_householdContext.HasHousehold || _householdContext.HouseholdId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Active household context is required.");
        }
    }

    private string GetCurrentUserId() => _currentUser.UserId!;

    private void EnsureAccountWriteAccess(Account account, string currentUserId)
    {
        if (account.OwnershipType == OwnershipType.Individual)
        {
            bool isOwner = string.Equals(account.OwnerUserId, currentUserId, StringComparison.Ordinal);
            bool isAdmin = _householdContext.Role == HouseholdRole.Admin;

            if (!isOwner && !isAdmin)
            {
                throw new UnauthorizedAccessException(
                    "Only the account owner or a household admin may modify this individual account.");
            }
        }
    }

    private static AccountDto MapToDto(Account a, DateOnly today)
    {
        var latestValuation = a.LatestValuation;
        bool isStale = false;

        if (latestValuation != null)
        {
            int daysSinceValuation = today.DayNumber - latestValuation.ValuationDate.DayNumber;
            isStale = daysSinceValuation > StalenessThresholdDays;
        }

        return new AccountDto(
            a.Id,
            a.HouseholdId,
            a.Name,
            a.AccountType,
            a.OwnershipType,
            a.OwnerUserId,
            a.CurrencyCode,
            a.IsActive,
            a.CreatedByUserId,
            a.CreatedAt,
            a.UpdatedAt,
            latestValuation?.Amount,
            latestValuation?.ValuationDate,
            isStale);
    }
}
