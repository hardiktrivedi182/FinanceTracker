using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceTracker.Infrastructure.Tests.Persistence;

public class FinanceTrackerDbContextTests
{
    private static DbContextOptions<FinanceTrackerDbContext> CreateNewContextOptions() =>
        new DbContextOptionsBuilder<FinanceTrackerDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
            .Options;

    [Fact]
    public void DbContext_CanBeCreated_WithInMemoryProvider()
    {
        var options = CreateNewContextOptions();
        using var context = new FinanceTrackerDbContext(options);

        context.Should().NotBeNull();
        context.Database.EnsureCreated().Should().BeTrue();
    }

    [Fact]
    public async Task CanPersistAndRetrieveHouseholdWithMembersAndAccounts()
    {
        var options = CreateNewContextOptions();
        var householdId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var context = new FinanceTrackerDbContext(options))
        {
            var household = new Household(householdId, "Our Household", now);
            var member = new HouseholdMember(householdId, "user-partner-1", HouseholdRole.Admin, now);
            var jointAccount = new Account(
                Guid.NewGuid(),
                householdId,
                "Joint Checking",
                AccountType.Checking,
                OwnershipType.Joint,
                null,
                "USD",
                "user-partner-1",
                now);

            var individualAccount = new Account(
                Guid.NewGuid(),
                householdId,
                "Partner 1 Savings",
                AccountType.Savings,
                OwnershipType.Individual,
                "user-partner-1",
                "USD",
                "user-partner-1",
                now);

            context.Households.Add(household);
            context.HouseholdMembers.Add(member);
            context.Accounts.AddRange(jointAccount, individualAccount);

            await context.SaveChangesAsync();
        }

        await using (var context = new FinanceTrackerDbContext(options))
        {
            var retrievedHousehold = await context.Households
                .Include(h => h.Members)
                .Include(h => h.Accounts)
                .FirstOrDefaultAsync(h => h.Id == householdId);

            retrievedHousehold.Should().NotBeNull();
            retrievedHousehold!.Name.Should().Be("Our Household");
            retrievedHousehold.Members.Should().HaveCount(1);
            retrievedHousehold.Members.First().UserId.Should().Be("user-partner-1");
            retrievedHousehold.Members.First().Role.Should().Be(HouseholdRole.Admin);

            retrievedHousehold.Accounts.Should().HaveCount(2);
            var joint = retrievedHousehold.Accounts.First(a => a.OwnershipType == OwnershipType.Joint);
            joint.Name.Should().Be("Joint Checking");
            joint.OwnerUserId.Should().BeNull();

            var individual = retrievedHousehold.Accounts.First(a => a.OwnershipType == OwnershipType.Individual);
            individual.Name.Should().Be("Partner 1 Savings");
            individual.OwnerUserId.Should().Be("user-partner-1");
        }
    }
}
