using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Infrastructure.Identity;

/// <summary>
/// The ASP.NET Core Identity user for FinanceTracker.
/// Extended to record which household this user belongs to and their role within it.
/// Household membership is set at account creation time and cannot be changed by the user.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>The household this user is a member of.</summary>
    public Guid HouseholdId { get; set; }

    /// <summary>Display name shown in the UI (not used for authentication).</summary>
    public string? DisplayName { get; set; }
}
