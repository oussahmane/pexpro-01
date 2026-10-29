using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Company;
using Pexilio.Domain.Stock;
using Pexilio.Infrastructure.Auth;
using Pexilio.Infrastructure.Identity;

namespace Pexilio.Infrastructure.Persistence;

public sealed class PexilioDbContext(DbContextOptions<PexilioDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<TaxRate> TaxRates => Set<TaxRate>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockLocation> StockLocations => Set<StockLocation>();

    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<StockIdempotency> StockIdempotencies => Set<StockIdempotency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("pexilio");
        modelBuilder.Entity<AppUser>().ToTable("AspNetUsers", "pexilio");
        modelBuilder.Entity<AppRole>().ToTable("AspNetRoles", "pexilio");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("AspNetUserRoles", "pexilio");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("AspNetUserClaims", "pexilio");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("AspNetUserLogins", "pexilio");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("AspNetUserTokens", "pexilio");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("AspNetRoleClaims", "pexilio");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PexilioDbContext).Assembly);
    }
}
