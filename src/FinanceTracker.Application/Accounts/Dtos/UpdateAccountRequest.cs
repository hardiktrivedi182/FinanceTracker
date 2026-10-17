using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Dtos;

public record UpdateAccountRequest(
    string Name,
    AccountType AccountType,
    OwnershipType OwnershipType,
    string? OwnerUserId);
