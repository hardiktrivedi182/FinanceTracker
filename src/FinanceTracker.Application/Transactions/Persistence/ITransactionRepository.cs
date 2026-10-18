using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Transactions.Persistence;

/// <summary>
/// Persistence contract for committed ledger transactions.
/// All queries are household-scoped.
/// </summary>
public interface ITransactionRepository
{
    Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken ct = default);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByHouseholdIdAsync(
        Guid householdId,
        Guid? accountId = null,
        Guid? importBatchId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByFingerprintsAsync(
        Guid accountId,
        IEnumerable<string> fingerprints,
        CancellationToken ct = default);
}
