using FinanceTracker.Application.Statements.Persistence;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence.Repositories;

public class StatementRepository : IStatementRepository
{
    private readonly FinanceTrackerDbContext _context;

    public StatementRepository(FinanceTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<Statement?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Statements
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<Statement>> GetByHouseholdIdAsync(Guid householdId, CancellationToken ct = default)
    {
        return await _context.Statements
            .AsNoTracking()
            .Where(s => s.HouseholdId == householdId)
            .OrderByDescending(s => s.UploadedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Statement statement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statement);
        _context.Statements.Add(statement);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Statement statement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statement);
        _context.Statements.Update(statement);
        await _context.SaveChangesAsync(ct);
    }
}
