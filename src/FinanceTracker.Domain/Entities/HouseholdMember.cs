using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Associates an authenticated user with a household and their permission role.
/// </summary>
public class HouseholdMember
{
    // Parameterless constructor for EF Core
    private HouseholdMember()
    {
        UserId = string.Empty;
    }

    public HouseholdMember(Guid householdId, string userId, HouseholdRole role, DateTimeOffset joinedAt)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household Id cannot be empty.", nameof(householdId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        string trimmedUserId = userId.Trim();
        if (trimmedUserId.Length > 450)
        {
            throw new ArgumentException("User Id cannot exceed 450 characters.", nameof(userId));
        }

        HouseholdId = householdId;
        UserId = trimmedUserId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid HouseholdId { get; private set; }
    public string UserId { get; private set; }
    public HouseholdRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    // Navigation property
    public Household Household { get; private set; } = null!;

    public void UpdateRole(HouseholdRole newRole)
    {
        Role = newRole;
    }
}
