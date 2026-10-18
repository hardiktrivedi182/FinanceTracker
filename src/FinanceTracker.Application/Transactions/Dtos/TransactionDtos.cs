using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Transactions.Dtos;

public record TransactionDto(
    Guid Id,
    Guid HouseholdId,
    Guid AccountId,
    Guid? StatementId,
    Guid? ImportBatchId,
    int? SourceRowNumber,
    DateOnly TransactionDate,
    DateOnly OriginalTransactionDate,
    string Description,
    decimal Amount,
    TransactionType Type,
    decimal SignedAmount,
    string CurrencyCode,
    string Fingerprint,
    DateTimeOffset CreatedAt);
