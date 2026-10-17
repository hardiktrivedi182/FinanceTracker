namespace FinanceTracker.Application.Common.Interfaces;

/// <summary>
/// Provides information about the currently authenticated user.
/// Decoupled from ASP.NET Core HttpContext for testability and portability.
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
