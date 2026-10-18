using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Statements.Persistence;

/// <summary>
/// Persistence contract for statement ingestion records.
/// All queries are household-scoped.
/// </summary>
public interface IStatementRepository
{
    Task<Statement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Statement>> GetByHouseholdIdAsync(Guid householdId, CancellationToken ct = default);
    Task AddAsync(Statement statement, CancellationToken ct = default);
    Task UpdateAsync(Statement statement, CancellationToken ct = default);
}
