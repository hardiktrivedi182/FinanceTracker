using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Accounts.Persistence;

/// <summary>
/// Persistence contract for Account aggregates within a household boundary.
/// </summary>
public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> GetByHouseholdIdAsync(Guid householdId, bool includeInactive, CancellationToken ct = default);
    Task<Account?> GetByIdAsync(Guid householdId, Guid id, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task UpdateAsync(Account account, CancellationToken ct = default);
    Task AddValuationAsync(AccountValuation valuation, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
