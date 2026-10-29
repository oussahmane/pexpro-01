using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Api.IntegrationTests;

public sealed class HealthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public HealthApiFactory(string connectionString)
    {
        DatabaseGuard.EnsureTestDatabase(connectionString);
        _connectionString = connectionString;
        Environment.SetEnvironmentVariable("PEXILIO_CONNECTION", connectionString);
        Environment.SetEnvironmentVariable("PEXILIO_JWT_SIGNING_KEY", TestDatabase.SigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var existing = services.Where(descriptor =>
                descriptor.ServiceType == typeof(PexilioDbContext)
                || descriptor.ServiceType == typeof(DbContextOptions<PexilioDbContext>)).ToList();

            foreach (var descriptor in existing)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<PexilioDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "pexilio")));
        });
    }
}
