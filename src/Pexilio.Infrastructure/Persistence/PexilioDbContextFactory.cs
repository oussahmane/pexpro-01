using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pexilio.Infrastructure.Persistence;

public sealed class PexilioDbContextFactory : IDesignTimeDbContextFactory<PexilioDbContext>
{
    public PexilioDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PEXILIO_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set PEXILIO_CONNECTION before running Entity Framework Core tools.");
        }

        var options = new DbContextOptionsBuilder<PexilioDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "pexilio"))
            .Options;

        return new PexilioDbContext(options);
    }
}
