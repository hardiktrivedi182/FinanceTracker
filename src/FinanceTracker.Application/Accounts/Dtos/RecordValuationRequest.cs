using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.Dtos;

public record RecordValuationRequest(
    DateOnly ValuationDate,
    decimal Amount,
    string? Notes = null,
    string? SourceReference = null,
    ValuationSourceType SourceType = ValuationSourceType.Manual);
