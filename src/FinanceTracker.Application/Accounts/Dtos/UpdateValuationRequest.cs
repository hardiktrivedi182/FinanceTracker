using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Dtos;

public record UpdateValuationRequest(
    DateOnly ValuationDate,
    decimal Amount,
    ValuationSourceType SourceType = ValuationSourceType.Manual,
    string? Notes = null,
    string? SourceReference = null);
