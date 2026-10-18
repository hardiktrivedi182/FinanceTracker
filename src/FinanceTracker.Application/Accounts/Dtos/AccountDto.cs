using System.Text.Json.Serialization;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Dtos;

public record AccountDto(
    Guid Id,
    Guid HouseholdId,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] AccountType AccountType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] OwnershipType OwnershipType,
    string? OwnerUserId,
    string CurrencyCode,
    bool IsActive,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    decimal? LatestBalance,
    DateOnly? LatestValuationDate,
    bool IsStale);
