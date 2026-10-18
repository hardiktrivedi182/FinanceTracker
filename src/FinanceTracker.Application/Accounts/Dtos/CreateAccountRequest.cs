using System.Text.Json.Serialization;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Dtos;

public record CreateAccountRequest(
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] AccountType AccountType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] OwnershipType OwnershipType,
    string? OwnerUserId,
    string CurrencyCode,
    decimal? InitialBalance = null,
    DateOnly? InitialValuationDate = null);
