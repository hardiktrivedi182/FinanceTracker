using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.ImportBatches.Persistence;

/// <summary>
/// Persistence contract for statement import batches and staged rows.
/// All queries are household-scoped.
/// </summary>
public interface IImportBatchRepository
{
    Task AddAsync(ImportBatch batch, CancellationToken ct = default);
    Task<ImportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ImportBatch?> GetByIdWithRowsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatch>> GetByHouseholdIdAsync(Guid householdId, Guid? statementId = null, CancellationToken ct = default);
    Task<ImportBatch?> GetLatestByStatementIdAsync(Guid statementId, CancellationToken ct = default);
    Task UpdateAsync(ImportBatch batch, CancellationToken ct = default);
    Task CommitBatchAtomicAsync(ImportBatch batch, IReadOnlyList<Transaction> transactions, Statement? statement, CancellationToken ct = default);
}
