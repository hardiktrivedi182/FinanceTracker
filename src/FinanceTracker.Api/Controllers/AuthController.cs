using FinanceTracker.Api.Security;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly IHouseholdContext _householdContext;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ICurrentUser currentUser,
        IHouseholdContext householdContext,
        IWebHostEnvironment env)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _currentUser = currentUser;
        _householdContext = householdContext;
        _env = env;
    }

    /// <summary>
    /// Returns the currently authenticated user's identity.
    /// Returns 200 with isAuthenticated=false when no session is active.
    /// </summary>
    [HttpGet("me")]
    [AllowAnonymous]
    public IActionResult GetCurrentUser()
    {
        return Ok(new
        {
            userId = _currentUser.UserId,
            email = _currentUser.Email,
            isAuthenticated = _currentUser.IsAuthenticated,
            householdId = _currentUser.IsAuthenticated ? _householdContext.HouseholdId : (Guid?)null,
            householdRole = _currentUser.IsAuthenticated ? _householdContext.Role.ToString() : null,
        });
    }

    /// <summary>
    /// Authenticates the user with email and password.
    /// Issues a secure HttpOnly session cookie on success.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Use the same message as a failed password to prevent user enumeration.
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Account temporarily locked. Please try again later." });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            householdId = user.HouseholdId,
            message = "Signed in successfully.",
        });
    }

    /// <summary>
    /// Signs the current user out and invalidates their session cookie.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Signed out." });
    }

    // ─── Development-only endpoints ──────────────────────────────────────────
    // These endpoints are gated by an environment check at the HTTP level.
    // They must never appear in Staging or Production.

    /// <summary>
    /// Development-only: set a dev session cookie for the specified known test user.
    /// Returns 404 in non-Development environments.
    /// </summary>
    [HttpPost("dev-login")]
    [AllowAnonymous]
    public IActionResult DevLogin([FromBody] DevLoginRequest request)
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        // Only allow known test user IDs.
        var allowedUsers = new[] { "partner-1", "partner-2" };
        if (!allowedUsers.Contains(request.UserId, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Unknown dev user: '{request.UserId}'." });
        }

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false, // Allowed for local HTTP dev server only.
            Expires = DateTimeOffset.UtcNow.AddHours(8),
        };

        Response.Cookies.Append("dev_user_id", request.UserId, cookieOptions);

        return Ok(new
        {
            userId = request.UserId,
            householdId = HouseholdContext.DefaultHouseholdId,
            message = $"Dev session started as '{request.UserId}'.",
        });
    }
}

public record LoginRequest(string Email, string Password);
public record DevLoginRequest(string UserId);
