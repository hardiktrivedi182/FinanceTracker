using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// The main EF Core database context for FinanceTracker.
/// Extends IdentityDbContext to host ASP.NET Core Identity tables alongside domain tables.
/// Configured in the Infrastructure layer; consumed by the API host.
/// </summary>
public class FinanceTrackerDbContext : IdentityDbContext<ApplicationUser>
{
    public FinanceTrackerDbContext(DbContextOptions<FinanceTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountValuation> AccountValuations => Set<AccountValuation>();
    public DbSet<Statement> Statements => Set<Statement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Prefix all Identity tables to keep them visually separate from domain tables.
        builder.Entity<ApplicationUser>().ToTable("identity_users");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("identity_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("identity_user_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("identity_user_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("identity_user_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("identity_user_tokens");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("identity_role_claims");

        builder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerDbContext).Assembly);
    }
}
