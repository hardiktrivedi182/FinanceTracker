using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using FinanceTracker.Infrastructure.Persistence;
using Xunit;

namespace FinanceTracker.Infrastructure.Tests.Persistence;

public class FinanceTrackerDbContextTests
{
    [Fact]
    public void DbContext_CanBeCreated_WithInMemoryProvider()
    {
        var options = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
            .Options;

        using var context = new FinanceTrackerDbContext(options);

        context.Should().NotBeNull();
        context.Database.EnsureCreated().Should().BeTrue();
    }
}
