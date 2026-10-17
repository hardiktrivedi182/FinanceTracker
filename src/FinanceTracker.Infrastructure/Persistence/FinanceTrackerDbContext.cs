using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// The main EF Core database context for FinanceTracker.
/// Configured in the Infrastructure layer; consumed by the API host.
/// </summary>
public class FinanceTrackerDbContext : DbContext
{
    public FinanceTrackerDbContext(DbContextOptions<FinanceTrackerDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity configurations will be added here as entities are defined.
        // Convention: use decimal(19,4) for all monetary columns.
    }
}
