using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Provides tenancy and household context for the current request.
/// Enforces multi-user household boundary on the server.
/// </summary>
public interface IHouseholdContext
{
    Guid HouseholdId { get; }
    HouseholdRole Role { get; }
    bool HasHousehold { get; }
}
