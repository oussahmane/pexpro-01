using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pexilio.Application.Health;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Api.IntegrationTests;

public sealed class HealthEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private HealthApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new HealthApiFactory(LocalEnvironment.TestConnectionString());
        await TestDatabase.PrepareAsync(_factory);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Returns_healthy_when_the_database_accepts_connections()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthSnapshot>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(HealthEvaluation.Healthy, body.Status);
        Assert.Equal(HealthEvaluation.DatabaseUp, body.Database);
    }

    [Fact]
    public async Task Applies_the_initial_migration()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var applied = await database.Database.GetAppliedMigrationsAsync();

        Assert.Contains(applied, id => id.Contains("InitialCreate", StringComparison.Ordinal));
    }
}

public sealed class UnreachableDatabaseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task Returns_unhealthy_when_the_database_refuses_connections()
    {
        await using var factory = new HealthApiFactory(
            "Host=127.0.0.1;Port=1;Database=pexilio_test;Username=pexilio_dev;Password=invalid;Timeout=2");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthSnapshot>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(HealthEvaluation.Unhealthy, body.Status);
        Assert.Equal(HealthEvaluation.DatabaseDown, body.Database);
    }
}
