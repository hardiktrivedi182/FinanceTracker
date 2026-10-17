using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Dtos;

public record CreateAccountRequest(
    string Name,
    AccountType AccountType,
    OwnershipType OwnershipType,
    string? OwnerUserId,
    string CurrencyCode,
    decimal? InitialBalance = null,
    DateOnly? InitialValuationDate = null);
