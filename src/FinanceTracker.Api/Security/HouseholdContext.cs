using System.Security.Claims;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Api.Security;

/// <summary>
/// Reads the current user's household context from authenticated claims only.
/// Header-based fallbacks are intentionally absent; household membership and role
/// are established exclusively by the authentication middleware and cannot be
/// overridden by a caller through request headers.
/// </summary>
public class HouseholdContext : IHouseholdContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public static readonly Guid DefaultHouseholdId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public HouseholdContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid HouseholdId
    {
        get
        {
            var claimValue = _httpContextAccessor.HttpContext?.User?.FindFirstValue("household_id");
            return Guid.TryParse(claimValue, out var claimGuid) ? claimGuid : Guid.Empty;
        }
    }

    public HouseholdRole Role
    {
        get
        {
            var claimRole = _httpContextAccessor.HttpContext?.User?.FindFirstValue("household_role");
            return Enum.TryParse<HouseholdRole>(claimRole, ignoreCase: true, out var parsedRole)
                ? parsedRole
                : HouseholdRole.Member;
        }
    }

    public bool HasHousehold => HouseholdId != Guid.Empty;
}
