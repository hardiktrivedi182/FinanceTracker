using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Security;

/// <summary>
/// A development-only authentication handler that accepts a user ID from a cookie or header.
/// Household membership and role are fixed per known test user — they are NOT read from
/// request headers, so callers cannot manipulate household assignment even in development.
///
/// This handler is registered as the default scheme only when
/// <c>ASPNETCORE_ENVIRONMENT == Development</c>. It must never be active in production.
/// </summary>
public class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevAuth";

    // Fixed test-user definitions for local development.
    // Only user IDs in this map are accepted; unknown IDs are rejected.
    private static readonly Dictionary<string, (string Email, string Role)> KnownTestUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["partner-1"] = ("partner1@household.local", "Admin"),
        ["partner-2"] = ("partner2@household.local", "Member"),
    };

    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Accept user ID from cookie first, then header (header is a fallback for curl/Swagger convenience only).
        var userId = Request.Cookies.TryGetValue("dev_user_id", out var cookieUser) && !string.IsNullOrWhiteSpace(cookieUser)
            ? cookieUser
            : Request.Headers["X-User-Id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Only recognise known test users; reject arbitrary user IDs.
        if (!KnownTestUsers.TryGetValue(userId, out var userInfo))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Unknown dev user: '{userId}'."));
        }

        // household_id and household_role are fixed from the test-user map —
        // they are NOT taken from request headers.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId),
            new Claim(ClaimTypes.Email, userInfo.Email),
            new Claim("household_id", HouseholdContext.DefaultHouseholdId.ToString()),
            new Claim("household_role", userInfo.Role),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
