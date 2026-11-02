using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pexilio.Application;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Api.IntegrationTests;

public sealed class CustomerApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HealthApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new HealthApiFactory(LocalEnvironment.TestConnectionString());
        await TestDatabase.PrepareAsync(_factory);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Duplicate_names_remain_distinct_and_search_uses_the_normalized_key()
    {
        using var client = await AdministratorClientAsync();
        var first = await CreateAsync(client, new { type = "individual", name = "Amin", phone = "0551000001" });
        var second = await CreateAsync(client, new { type = "business", name = " amin ", phone = "0551000002" });
        var listed = await client.GetFromJsonAsync<List<CustomerResponse>>("/api/v1/customers?q=amin", JsonOptions);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Amin", first.Name);
        Assert.Equal("amin", second.Name);
        Assert.Equal(2, listed!.Count);
        Assert.Contains(listed, customer => customer.Id == first.Id && customer.Phone == "0551000001");
        Assert.Contains(listed, customer => customer.Id == second.Id && customer.Type == "business");
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var keys = await database.Customers.Select(customer => customer.NameKey).ToListAsync();
        Assert.Equal(2, keys.Count);
        Assert.All(keys, key => Assert.Equal("AMIN", key));
    }

    [Fact]
    public async Task A_cashier_edits_contact_fields_and_cannot_change_restricted_fields()
    {
        using var admin = await AdministratorClientAsync();
        var created = await CreateAsync(admin, new
        {
            type = "business",
            name = "Atlas",
            phone = "0551000001",
            nif = "0001",
            creditLimit = "50.0000",
            isActive = true,
        });
        await admin.PostAsJsonAsync("/api/v1/users", new { email = "cashier.customer@example.com", password = "cashier-password-1" });
        using var cashier = await LoginAsync("cashier.customer@example.com", "cashier-password-1");

        var forbidden = await cashier.PutAsJsonAsync($"/api/v1/customers/{created.Id}", new
        {
            type = "business",
            name = "Atlas",
            phone = "0551000001",
            nif = "9999",
            creditLimit = "1.0000",
            isActive = false,
        });
        var edited = await cashier.PutAsJsonAsync($"/api/v1/customers/{created.Id}", new
        {
            type = "business",
            name = "Atlas Eau",
            phone = "0551000099",
            address = "Rue 2",
            notes = "Comptoir",
        });
        var stored = await cashier.GetFromJsonAsync<CustomerResponse>($"/api/v1/customers/{created.Id}", JsonOptions);
        var individual = await SendAsync(cashier, new { type = "individual", name = "Sara", nif = "1" });

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("Atlas Eau", stored!.Name);
        Assert.Equal("0551000099", stored.Phone);
        Assert.Equal("Rue 2", stored.Address);
        Assert.Equal("Comptoir", stored.Notes);
        Assert.Equal("0001", stored.Nif);
        Assert.Equal("50.0000", stored.CreditLimit);
        Assert.True(stored.IsActive);
        Assert.Equal(HttpStatusCode.BadRequest, individual.StatusCode);
        Assert.Equal("customer.identifier", await FieldAsync(individual));
    }

    [Fact]
    public async Task An_administrator_can_deactivate_a_business_and_queries_stay_inside_the_company()
    {
        using var client = await AdministratorClientAsync();
        var created = await CreateAsync(client, new { type = "business", name = "Atlas", rc = "12 B 34", nif = "0001" });
        var deactivated = await client.PutAsJsonAsync($"/api/v1/customers/{created.Id}", new
        {
            type = "business",
            name = "Atlas",
            rc = "12 B 34",
            nif = "0001",
            isActive = false,
        });
        var stored = await deactivated.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        var missing = await client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.False(stored!.IsActive);
        Assert.Equal("12 B 34", stored.Rc);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var companyId = await database.Companies.Select(company => company.Id).SingleAsync();
        Assert.Equal(companyId, await database.Customers.Select(customer => customer.CompanyId).SingleAsync());
    }

    private async Task<HttpClient> AdministratorClientAsync()
    {
        var client = _factory.CreateClient();
        var setup = await client.PostAsJsonAsync("/api/v1/setup", new
        {
            legalName = "Pexilio Démo",
            defaultTaxRate = "0.19",
            email = "owner@example.com",
            password = "correct-horse-battery",
        });
        if (setup.StatusCode != HttpStatusCode.Conflict)
        {
            Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        }

        return await LoginAsync("owner@example.com", "correct-horse-battery");
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var tokens = await login.Content.ReadFromJsonAsync<TokenPair>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private static async Task<CustomerResponse> CreateAsync(HttpClient client, object body)
    {
        var response = await SendAsync(client, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions))!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, object body)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(message);
    }

    private static async Task<string?> FieldAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ProblemBody>(JsonOptions);
        return body?.Errors?.Values.FirstOrDefault()?.FirstOrDefault();
    }

    private sealed record ProblemBody(string? Code, Dictionary<string, string[]>? Errors);
}
