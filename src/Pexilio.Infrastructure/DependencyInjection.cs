using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Pexilio.Application;
using Pexilio.Domain.Accounts;
using Pexilio.Infrastructure.Accounts;
using Pexilio.Infrastructure.Catalog;
using Pexilio.Infrastructure.Stock;
using Pexilio.Infrastructure.Auth;
using Pexilio.Infrastructure.Identity;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPexilio(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration["PEXILIO_CONNECTION"];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is missing. Set ConnectionStrings:Database or PEXILIO_CONNECTION.");
        }

        var signingKey = configuration["PEXILIO_JWT_SIGNING_KEY"];
        if (string.IsNullOrWhiteSpace(signingKey)
            || signingKey.Length < 32
            || signingKey is "replace-me" or "replace-with-generated-key")
        {
            throw new InvalidOperationException(
                "PEXILIO_JWT_SIGNING_KEY is missing or too short. Generate it in the development environment file.");
        }

        services.AddDbContext<PexilioDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "pexilio")));

        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = PasswordPolicy.MinimumLength;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<PexilioDbContext>();

        var auth = new AuthSettings { SigningKey = signingKey };
        services.AddSingleton(auth);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AccessTokenFactory>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ICashierService, CashierService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IStockService, StockService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub",
                    RoleClaimType = AccessTokenFactory.RoleClaim,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Administrator", policy => policy.RequireRole(RoleNames.Administrator));
            options.AddPolicy("products.read", policy => policy.RequireRole(RoleNames.Administrator, RoleNames.Cashier));
            options.AddPolicy("products.write", policy => policy.RequireRole(RoleNames.Administrator));
            options.AddPolicy("stock.read", policy => policy.RequireRole(RoleNames.Administrator, RoleNames.Cashier));
            options.AddPolicy("stock.adjust", policy => policy.RequireRole(RoleNames.Administrator));
        });

        return services;
    }
}
