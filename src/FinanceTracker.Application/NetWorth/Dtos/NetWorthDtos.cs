using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.NetWorth.Dtos;

public record HouseholdNetWorthReportDto(
    DateOnly AsOfDate,
    DateTimeOffset GeneratedAt,
    int StalenessThresholdDays,
    IReadOnlyList<CurrencyNetWorthDto> Currencies);

public record CurrencyNetWorthDto(
    string CurrencyCode,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal NetWorth,
    HouseholdOwnershipBreakdownDto OwnershipBreakdown,
    IReadOnlyList<AssetAllocationItemDto> AssetAllocation,
    IReadOnlyList<AccountNetWorthItemDto> Accounts);

public record HouseholdOwnershipBreakdownDto(
    decimal JointTotal,
    decimal Partner1IndividualTotal,
    decimal Partner2IndividualTotal,
    decimal Partner1TotalShare,
    decimal Partner2TotalShare);

public record AssetAllocationItemDto(
    AccountType AccountType,
    string AccountTypeName,
    decimal TotalAmount,
    decimal Percentage);

public record AccountNetWorthItemDto(
    Guid AccountId,
    string AccountName,
    AccountType AccountType,
    OwnershipType OwnershipType,
    string? OwnerUserId,
    bool IsLiability,
    DateOnly? ValuationDate,
    decimal Amount,
    bool IsStale,
    bool HasMissingValuation,
    ValuationSourceType? SourceType);

public record NetWorthComparisonDto(
    DateOnly Date1,
    DateOnly Date2,
    IReadOnlyList<CurrencyComparisonDto> Currencies);

public record CurrencyComparisonDto(
    string CurrencyCode,
    decimal Date1NetWorth,
    decimal Date2NetWorth,
    decimal AbsoluteChange,
    decimal? PercentageChange,
    decimal Date1Assets,
    decimal Date2Assets,
    decimal Date1Liabilities,
    decimal Date2Liabilities);
