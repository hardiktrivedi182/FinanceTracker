using FinanceTracker.Application.ImportBatches.Persistence;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories;

public class ImportBatchRepository : IImportBatchRepository
{
    private readonly FinanceTrackerDbContext _context;

    public ImportBatchRepository(FinanceTrackerDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ImportBatch batch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await _context.ImportBatches.AddAsync(batch, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ImportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<ImportBatch?> GetByIdWithRowsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ImportBatches
            .Include(b => b.Rows.OrderBy(r => r.SourceRowNumber))
            .Include(b => b.Statement)
            .Include(b => b.Account)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<IReadOnlyList<ImportBatch>> GetByHouseholdIdAsync(
        Guid householdId,
        Guid? statementId = null,
        CancellationToken ct = default)
    {
        var query = _context.ImportBatches
            .AsNoTracking()
            .Where(b => b.HouseholdId == householdId);

        if (statementId.HasValue)
        {
            query = query.Where(b => b.StatementId == statementId.Value);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ImportBatch?> GetLatestByStatementIdAsync(Guid statementId, CancellationToken ct = default)
    {
        return await _context.ImportBatches
            .Include(b => b.Rows.OrderBy(r => r.SourceRowNumber))
            .Include(b => b.Statement)
            .Include(b => b.Account)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(b => b.StatementId == statementId, ct);
    }

    public async Task UpdateAsync(ImportBatch batch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        _context.ImportBatches.Update(batch);
        await _context.SaveChangesAsync(ct);
    }

    public async Task CommitBatchAtomicAsync(
        ImportBatch batch,
        IReadOnlyList<Transaction> transactions,
        Statement? statement,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(transactions);

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (_context.Database.IsRelational())
            {
                await using var tx = await _context.Database.BeginTransactionAsync(ct);
                if (transactions.Count > 0)
                {
                    await _context.Transactions.AddRangeAsync(transactions, ct);
                }
                _context.ImportBatches.Update(batch);
                if (statement != null)
                {
                    _context.Statements.Update(statement);
                }
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            else
            {
                if (transactions.Count > 0)
                {
                    await _context.Transactions.AddRangeAsync(transactions, ct);
                }
                _context.ImportBatches.Update(batch);
                if (statement != null)
                {
                    _context.Statements.Update(statement);
                }
                await _context.SaveChangesAsync(ct);
            }
        });
    }
}
