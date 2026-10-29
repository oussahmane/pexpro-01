using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Company;
using Pexilio.Domain.Purchasing;
using Pexilio.Domain.Stock;
using Pexilio.Infrastructure.Auth;
using Pexilio.Infrastructure.Identity;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Api.IntegrationTests;

internal static class TestDatabase
{
    public const string SigningKey = "integration-test-signing-key-32b";

    public static async Task PrepareAsync(HealthApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var name = await CurrentDatabaseAsync(database);
        if (!string.Equals(name, "pexilio_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to prepare database '{name}'.");
        }

        await database.Database.MigrateAsync();
        await ClearApplicationDataAsync(database);
    }

    public static async Task ClearApplicationDataAsync(PexilioDbContext database)
    {
        var name = await CurrentDatabaseAsync(database);
        if (!string.Equals(name, "pexilio_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to delete rows from database '{name}'.");
        }

        await database.Set<PurchaseIdempotency>().ExecuteDeleteAsync();
        await database.Set<PurchaseLine>().ExecuteDeleteAsync();
        await database.Set<Purchase>().ExecuteDeleteAsync();
        await database.Set<Supplier>().ExecuteDeleteAsync();
        await database.Set<StockIdempotency>().ExecuteDeleteAsync();
        await using (var purge = await database.Database.BeginTransactionAsync())
        {
            // The append-only trigger blocks DELETE for every role, including the table owner.
            // This session disables it only after current_database() is pexilio_test, then turns it back on
            // before the transaction commits. A failure rolls the disable back with the transaction.
            await database.Database.ExecuteSqlRawAsync(
                """ALTER TABLE pexilio.stock_movement DISABLE TRIGGER USER""");
            await database.Set<StockMovement>().ExecuteDeleteAsync();
            await database.Database.ExecuteSqlRawAsync(
                """ALTER TABLE pexilio.stock_movement ENABLE TRIGGER USER""");
            await purge.CommitAsync();
        }

        await database.Set<StockBalance>().ExecuteDeleteAsync();
        await database.Set<StockLocation>().ExecuteDeleteAsync();
        await database.Set<Product>().ExecuteDeleteAsync();
        await database.Database.ExecuteSqlRawAsync("""UPDATE pexilio.category SET "ParentId" = NULL""");
        await database.Set<Category>().ExecuteDeleteAsync();
        await database.RefreshTokens.ExecuteDeleteAsync();
        await database.Set<IdentityUserLogin<Guid>>().ExecuteDeleteAsync();
        await database.Set<IdentityUserToken<Guid>>().ExecuteDeleteAsync();
        await database.Set<IdentityUserClaim<Guid>>().ExecuteDeleteAsync();
        await database.Set<IdentityUserRole<Guid>>().ExecuteDeleteAsync();
        await database.Set<IdentityRoleClaim<Guid>>().ExecuteDeleteAsync();
        await database.Set<AppUser>().ExecuteDeleteAsync();
        await database.Set<AppRole>().ExecuteDeleteAsync();
        await database.Set<TaxRate>().ExecuteDeleteAsync();
        await database.Set<Company>().ExecuteDeleteAsync();
    }

    private static async Task<string> CurrentDatabaseAsync(PexilioDbContext database)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT current_database()";
        var value = await command.ExecuteScalarAsync();
        return value as string ?? "";
    }
}
