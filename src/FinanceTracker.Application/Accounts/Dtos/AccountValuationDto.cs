using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Dtos;

public record AccountValuationDto(
    Guid Id,
    Guid AccountId,
    DateOnly ValuationDate,
    decimal Amount,
    string CurrencyCode,
    ValuationSourceType SourceType,
    string? SourceReference,
    string? Notes,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    string? UpdatedByUserId = null,
    DateTimeOffset? UpdatedAt = null);
