using FinanceTracker.Application.Transactions.Persistence;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly FinanceTrackerDbContext _context;

    public TransactionRepository(FinanceTrackerDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        await _context.Transactions.AddRangeAsync(transactions, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetByHouseholdIdAsync(
        Guid householdId,
        Guid? accountId = null,
        Guid? importBatchId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken ct = default)
    {
        var query = _context.Transactions
            .AsNoTracking()
            .Where(t => t.HouseholdId == householdId);

        if (accountId.HasValue)
        {
            query = query.Where(t => t.AccountId == accountId.Value);
        }

        if (importBatchId.HasValue)
        {
            query = query.Where(t => t.ImportBatchId == importBatchId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= endDate.Value);
        }

        return await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetByFingerprintsAsync(
        Guid accountId,
        IEnumerable<string> fingerprints,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);
        var fingerprintList = fingerprints.ToList();
        if (fingerprintList.Count == 0)
        {
            return Array.Empty<Transaction>();
        }

        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId && fingerprintList.Contains(t.Fingerprint))
            .ToListAsync(ct);
    }
}
