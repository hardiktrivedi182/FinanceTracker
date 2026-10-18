using FinanceTracker.Application.Transactions.Dtos;

namespace FinanceTracker.Application.Transactions.Services;

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(
        Guid? accountId = null,
        Guid? importBatchId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken ct = default);
}
