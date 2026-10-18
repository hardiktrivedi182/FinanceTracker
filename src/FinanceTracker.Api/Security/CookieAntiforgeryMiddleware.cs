using Microsoft.AspNetCore.Antiforgery;

namespace FinanceTracker.Api.Security;

/// <summary>
/// Enforces Antiforgery validation on state-changing HTTP requests that are authenticated
/// via ambient browser cookies (ft_auth or dev_user_id).
/// Non-cookie requests (such as API clients or Swagger curl using developer headers) are excluded.
/// </summary>
public class CookieAntiforgeryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;
    private static readonly HashSet<string> StateChangingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Delete,
        HttpMethods.Patch
    };

    public CookieAntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        _next = next;
        _antiforgery = antiforgery;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (StateChangingMethods.Contains(context.Request.Method))
        {
            var isCookieAuthenticated = context.Request.Cookies.ContainsKey("ft_auth") ||
                                        context.Request.Cookies.ContainsKey("dev_user_id");

            var path = context.Request.Path.Value ?? string.Empty;
            var isAuthEndpoint = path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase);

            if (isCookieAuthenticated && !isAuthEndpoint)
            {
                try
                {
                    await _antiforgery.ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException ex)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Antiforgery validation failed. The required CSRF token is missing or invalid.",
                        detail = ex.Message
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}
