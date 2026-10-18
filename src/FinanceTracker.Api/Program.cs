using System.Security.Claims;
using FinanceTracker.Api.Security;
using FinanceTracker.Application.Accounts.Persistence;
using FinanceTracker.Application.Accounts.Services;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.NetWorth.Services;
using FinanceTracker.Application.Statements.Parsing;
using FinanceTracker.Application.Statements.Persistence;
using FinanceTracker.Application.Statements.Services;
using FinanceTracker.Application.Statements.Storage;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Importing.Csv;
using FinanceTracker.Infrastructure.Identity;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FinanceTracker.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── HTTP and Security services ──────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IHouseholdContext, HouseholdContext>();

// ─── Application and Infrastructure services ─────────────────────────────────
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<INetWorthService, NetWorthService>();
builder.Services.AddScoped<IStatementRepository, StatementRepository>();
builder.Services.AddScoped<ICsvStatementParser, CsvStatementParser>();
builder.Services.AddScoped<IStatementService, StatementService>();
builder.Services.AddSingleton(TimeProvider.System);

// ─── Statement Storage Provider ──────────────────────────────────────────────
builder.Services.Configure<LocalStorageOptions>(builder.Configuration.GetSection(LocalStorageOptions.SectionName));
builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));

var storageProvider = builder.Configuration["StatementStorage:Provider"] ?? "Local";
if (string.Equals(storageProvider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IStatementStorage, AzureBlobStatementStorage>();
}
else
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("Production environment requires AzureBlob statement storage. LocalStorage is not permitted in production.");
    }
    builder.Services.AddScoped<IStatementStorage, LocalStorageStatementStorage>();
}

// ─── Database ─────────────────────────────────────────────────────────────────
// In-memory provider for development/testing only.
// Production configuration will supply a SQL Server connection string via Key Vault.
builder.Services.AddDbContext<FinanceTrackerDbContext>(options =>
    options.UseInMemoryDatabase("FinanceTrackerDev"));

// ─── ASP.NET Core Identity ────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy: strong minimum for a private household app.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;

    // Account lockout: 5 failed attempts → 15-minute lockout.
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // Managed accounts — no self-registration.
    options.SignIn.RequireConfirmedEmail = false;

    // Require unique email addresses.
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<FinanceTrackerDbContext>()
.AddDefaultTokenProviders();

// ─── Authentication: cookie-based with Identity as default ───────────────────
var isDevelopment = builder.Environment.IsDevelopment();

// AddIdentity already configures the application cookie scheme internally.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ft_auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    // Require HTTPS except in local development.
    options.Cookie.SecurePolicy = isDevelopment
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    // Return 401/403 JSON instead of redirecting to a login page (API-first).
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// In Development, we provide a composite policy scheme that delegates to Identity if
// an ft_auth cookie is present, or to DevAuthenticationHandler otherwise.
// In Staging and Production, DevAuth and the composite scheme are NOT registered,
// ensuring only the hardened Identity application cookie scheme can authenticate requests.
const string DevCompositeScheme = "DevCompositeAuth";

if (isDevelopment)
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = DevCompositeScheme;
            options.DefaultAuthenticateScheme = DevCompositeScheme;
            options.DefaultChallengeScheme = DevCompositeScheme;
        })
        .AddPolicyScheme(DevCompositeScheme, "Development Composite Auth", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Cookies.ContainsKey("ft_auth"))
                {
                    return IdentityConstants.ApplicationScheme;
                }
                return DevAuthenticationHandler.SchemeName;
            };
        })
        .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
            DevAuthenticationHandler.SchemeName, null);
}

// Antiforgery protection for cookie-authenticated state-changing endpoints.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-Token";
    options.Cookie.Name = ".AspNetCore.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isDevelopment
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization();

// ─── MVC and API ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ─── CORS: Development only ──────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddHealthChecks();

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Seed development data ────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var householdId = HouseholdContext.DefaultHouseholdId;

    try
    {
        if (dbContext.Households.Find(householdId) == null)
        {
            var now = DateTimeOffset.UtcNow;
            var household = new Household(householdId, "Our Household", now);
            var partner1 = new HouseholdMember(householdId, "partner-1", HouseholdRole.Admin, now);
            var partner2 = new HouseholdMember(householdId, "partner-2", HouseholdRole.Member, now);

            dbContext.Households.Add(household);
            dbContext.HouseholdMembers.AddRange(partner1, partner2);
            await dbContext.SaveChangesAsync();
        }

        if (await userManager.FindByEmailAsync("partner1@household.local") == null)
        {
            var u1 = new ApplicationUser
            {
                Id = "partner-1",
                UserName = "partner1@household.local",
                Email = "partner1@household.local",
                HouseholdId = householdId,
                DisplayName = "Partner 1",
                EmailConfirmed = true,
            };
            await userManager.CreateAsync(u1, "Password123!");
            await userManager.AddClaimAsync(u1, new Claim("household_id", householdId.ToString()));
            await userManager.AddClaimAsync(u1, new Claim("household_role", "Admin"));
        }

        if (await userManager.FindByEmailAsync("partner2@household.local") == null)
        {
            var u2 = new ApplicationUser
            {
                Id = "partner-2",
                UserName = "partner2@household.local",
                Email = "partner2@household.local",
                HouseholdId = householdId,
                DisplayName = "Partner 2",
                EmailConfirmed = true,
            };
            await userManager.CreateAsync(u2, "Password123!");
            await userManager.AddClaimAsync(u2, new Claim("household_id", householdId.ToString()));
            await userManager.AddClaimAsync(u2, new Claim("household_role", "Member"));
        }
    }
    catch
    {
        // In-memory store may already be seeded across test factory instances.
    }
}

// ─── Middleware pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("FrontendDev");
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<CookieAntiforgeryMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/api/health");

app.Run();

// Make the implicit Program class accessible for integration tests.
public partial class Program
{
}
