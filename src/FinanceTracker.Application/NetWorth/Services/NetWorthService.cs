using FinanceTracker.Application.Accounts.Persistence;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.NetWorth.Dtos;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.NetWorth.Services;

public class NetWorthService : INetWorthService
{
    private readonly IAccountRepository _repository;
    private readonly IHouseholdContext _householdContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public NetWorthService(
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

    public async Task<HouseholdNetWorthReportDto> GetNetWorthAsync(
        DateOnly? asOfDate = null,
        int stalenessThresholdDays = 90,
        CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var now = _timeProvider.GetUtcNow();
        var targetDate = asOfDate ?? DateOnly.FromDateTime(now.UtcDateTime);

        var accounts = await _repository.GetByHouseholdIdAsync(
            _householdContext.HouseholdId,
            includeInactive: false,
            ct);

        var currencyReports = CalculateCurrenciesNetWorth(accounts, targetDate, stalenessThresholdDays);

        return new HouseholdNetWorthReportDto(
            targetDate,
            now,
            stalenessThresholdDays,
            currencyReports);
    }

    public async Task<NetWorthComparisonDto> CompareNetWorthAsync(
        DateOnly date1,
        DateOnly date2,
        int stalenessThresholdDays = 90,
        CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var accounts = await _repository.GetByHouseholdIdAsync(
            _householdContext.HouseholdId,
            includeInactive: false,
            ct);

        var report1 = CalculateCurrenciesNetWorth(accounts, date1, stalenessThresholdDays);
        var report2 = CalculateCurrenciesNetWorth(accounts, date2, stalenessThresholdDays);

        var allCurrencies = report1.Select(r => r.CurrencyCode)
            .Union(report2.Select(r => r.CurrencyCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var comparisons = new List<CurrencyComparisonDto>();

        foreach (var currency in allCurrencies)
        {
            var d1 = report1.FirstOrDefault(r => string.Equals(r.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase));
            var d2 = report2.FirstOrDefault(r => string.Equals(r.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase));

            var date1Net = d1?.NetWorth ?? 0m;
            var date2Net = d2?.NetWorth ?? 0m;
            var absChange = date2Net - date1Net;

            decimal? pctChange = null;
            if (date1Net != 0m)
            {
                pctChange = Math.Round((absChange / Math.Abs(date1Net)) * 100m, 2);
            }

            comparisons.Add(new CurrencyComparisonDto(
                currency,
                date1Net,
                date2Net,
                absChange,
                pctChange,
                d1?.TotalAssets ?? 0m,
                d2?.TotalAssets ?? 0m,
                d1?.TotalLiabilities ?? 0m,
                d2?.TotalLiabilities ?? 0m));
        }

        return new NetWorthComparisonDto(date1, date2, comparisons);
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

    private static List<CurrencyNetWorthDto> CalculateCurrenciesNetWorth(
        IReadOnlyList<Account> accounts,
        DateOnly targetDate,
        int stalenessThresholdDays)
    {
        var result = new List<CurrencyNetWorthDto>();

        var byCurrency = accounts
            .GroupBy(a => a.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key);

        foreach (var group in byCurrency)
        {
            var currencyCode = group.Key;
            var accountItems = new List<AccountNetWorthItemDto>();

            foreach (var account in group)
            {
                var val = account.GetValuationAsOf(targetDate);
                bool isLiability = account.AccountType.IsLiability();

                if (val != null)
                {
                    int daysSince = targetDate.DayNumber - val.ValuationDate.DayNumber;
                    bool isStale = daysSince > stalenessThresholdDays;

                    accountItems.Add(new AccountNetWorthItemDto(
                        account.Id,
                        account.Name,
                        account.AccountType,
                        account.OwnershipType,
                        account.OwnerUserId,
                        isLiability,
                        val.ValuationDate,
                        val.Amount,
                        isStale,
                        HasMissingValuation: false,
                        val.SourceType));
                }
                else
                {
                    // No valuation on or prior to targetDate
                    accountItems.Add(new AccountNetWorthItemDto(
                        account.Id,
                        account.Name,
                        account.AccountType,
                        account.OwnershipType,
                        account.OwnerUserId,
                        isLiability,
                        ValuationDate: null,
                        Amount: 0m,
                        IsStale: false,
                        HasMissingValuation: true,
                        SourceType: null));
                }
            }

            var totalAssets = accountItems
                .Where(a => !a.IsLiability)
                .Sum(a => a.Amount);

            var totalLiabilities = accountItems
                .Where(a => a.IsLiability)
                .Sum(a => a.Amount);

            var netWorth = totalAssets - totalLiabilities;

            // Ownership Breakdown
            var jointAssets = accountItems.Where(a => a.OwnershipType == OwnershipType.Joint && !a.IsLiability).Sum(a => a.Amount);
            var jointLiabilities = accountItems.Where(a => a.OwnershipType == OwnershipType.Joint && a.IsLiability).Sum(a => a.Amount);
            var jointTotal = jointAssets - jointLiabilities;

            // Dynamically identify individual owners, matching partner-1/partner-2 if present or falling back to distinct owners
            var distinctOwners = accountItems
                .Where(a => a.OwnershipType == OwnershipType.Individual && !string.IsNullOrWhiteSpace(a.OwnerUserId))
                .Select(a => a.OwnerUserId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? p1Id = distinctOwners.FirstOrDefault(u => string.Equals(u, "partner-1", StringComparison.OrdinalIgnoreCase))
                ?? distinctOwners.ElementAtOrDefault(0);
            string? p2Id = distinctOwners.FirstOrDefault(u => string.Equals(u, "partner-2", StringComparison.OrdinalIgnoreCase))
                ?? distinctOwners.FirstOrDefault(u => !string.Equals(u, p1Id, StringComparison.OrdinalIgnoreCase));

            var p1Assets = p1Id != null ? accountItems.Where(a => a.OwnershipType == OwnershipType.Individual && string.Equals(a.OwnerUserId, p1Id, StringComparison.OrdinalIgnoreCase) && !a.IsLiability).Sum(a => a.Amount) : 0m;
            var p1Liab = p1Id != null ? accountItems.Where(a => a.OwnershipType == OwnershipType.Individual && string.Equals(a.OwnerUserId, p1Id, StringComparison.OrdinalIgnoreCase) && a.IsLiability).Sum(a => a.Amount) : 0m;
            var p1Individual = p1Assets - p1Liab;

            var p2Assets = p2Id != null ? accountItems.Where(a => a.OwnershipType == OwnershipType.Individual && string.Equals(a.OwnerUserId, p2Id, StringComparison.OrdinalIgnoreCase) && !a.IsLiability).Sum(a => a.Amount) : 0m;
            var p2Liab = p2Id != null ? accountItems.Where(a => a.OwnershipType == OwnershipType.Individual && string.Equals(a.OwnerUserId, p2Id, StringComparison.OrdinalIgnoreCase) && a.IsLiability).Sum(a => a.Amount) : 0m;
            var p2Individual = p2Assets - p2Liab;

            // 50% joint allocation
            var p1TotalShare = p1Individual + Math.Round(jointTotal / 2m, 2);
            var p2TotalShare = p2Individual + (jointTotal - Math.Round(jointTotal / 2m, 2));

            var ownershipBreakdown = new HouseholdOwnershipBreakdownDto(
                jointTotal,
                p1Individual,
                p2Individual,
                p1TotalShare,
                p2TotalShare);

            // Asset Allocation
            var assetAllocation = accountItems
                .Where(a => !a.IsLiability && a.Amount > 0)
                .GroupBy(a => a.AccountType)
                .Select(g =>
                {
                    var typeTotal = g.Sum(a => a.Amount);
                    var pct = totalAssets > 0 ? Math.Round((typeTotal / totalAssets) * 100m, 2) : 0m;
                    return new AssetAllocationItemDto(g.Key, g.Key.ToString(), typeTotal, pct);
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            result.Add(new CurrencyNetWorthDto(
                currencyCode,
                totalAssets,
                totalLiabilities,
                netWorth,
                ownershipBreakdown,
                assetAllocation,
                accountItems));
        }

        return result;
    }
}
