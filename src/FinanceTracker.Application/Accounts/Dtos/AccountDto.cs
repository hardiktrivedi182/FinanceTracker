using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Dtos;

public record AccountDto(
    Guid Id,
    Guid HouseholdId,
    string Name,
    AccountType AccountType,
    OwnershipType OwnershipType,
    string? OwnerUserId,
    string CurrencyCode,
    bool IsActive,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    decimal? LatestBalance,
    DateOnly? LatestValuationDate,
    bool IsStale);
