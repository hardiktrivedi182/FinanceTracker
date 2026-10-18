using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by EF Core migration tools (e.g. dotnet ef migrations add).
/// Configures SQL Server provider without affecting runtime DI or In-Memory test execution.
/// </summary>
public class FinanceTrackerDbContextFactory : IDesignTimeDbContextFactory<FinanceTrackerDbContext>
{
    public FinanceTrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FinanceTrackerDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=FinanceTrackerDesign;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");

        return new FinanceTrackerDbContext(optionsBuilder.Options);
    }
}
