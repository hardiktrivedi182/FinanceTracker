using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceTracker.Domain.Tests.Entities;

public class HouseholdTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Household_Constructor_ValidParameters_CreatesInstance()
    {
        var id = Guid.NewGuid();
        var household = new Household(id, "The Smiths", _now);

        household.Id.Should().Be(id);
        household.Name.Should().Be("The Smiths");
        household.CreatedAt.Should().Be(_now);
        household.Members.Should().BeEmpty();
        household.Accounts.Should().BeEmpty();
    }

    [Fact]
    public void Household_UpdateName_ChangesName()
    {
        var household = new Household(Guid.NewGuid(), "Initial Name", _now);

        household.UpdateName("Updated Name");

        household.Name.Should().Be("Updated Name");
    }

    [Fact]
    public void HouseholdMember_Constructor_ValidParameters_CreatesInstance()
    {
        var householdId = Guid.NewGuid();
        var member = new HouseholdMember(householdId, "user-123", HouseholdRole.Admin, _now);

        member.HouseholdId.Should().Be(householdId);
        member.UserId.Should().Be("user-123");
        member.Role.Should().Be(HouseholdRole.Admin);
        member.JoinedAt.Should().Be(_now);
    }

    [Fact]
    public void HouseholdMember_UpdateRole_ChangesRole()
    {
        var member = new HouseholdMember(Guid.NewGuid(), "user-123", HouseholdRole.Member, _now);

        member.UpdateRole(HouseholdRole.Admin);

        member.Role.Should().Be(HouseholdRole.Admin);
    }
}
