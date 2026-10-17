using FinanceTracker.Application.Accounts.Persistence;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly FinanceTrackerDbContext _context;

    public AccountRepository(FinanceTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Account>> GetByHouseholdIdAsync(Guid householdId, bool includeInactive, CancellationToken ct = default)
    {
        var query = _context.Accounts
            .Include(a => a.Valuations)
            .Where(a => a.HouseholdId == householdId);

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        return await query
            .OrderBy(a => a.Name)
            .ToListAsync(ct);
    }

    public async Task<Account?> GetByIdAsync(Guid householdId, Guid id, CancellationToken ct = default)
    {
        return await _context.Accounts
            .Include(a => a.Valuations)
            .FirstOrDefaultAsync(a => a.HouseholdId == householdId && a.Id == id, ct);
    }

    public async Task AddAsync(Account account, CancellationToken ct = default)
    {
        await _context.Accounts.AddAsync(account, ct);
    }

    public Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        var entry = _context.Entry(account);
        if (entry.State == EntityState.Detached)
        {
            _context.Accounts.Update(account);
        }
        return Task.CompletedTask;
    }

    public async Task AddValuationAsync(AccountValuation valuation, CancellationToken ct = default)
    {
        await _context.AccountValuations.AddAsync(valuation, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
