using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pexilio.Application;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Api.IntegrationTests;

public sealed class WholesalePricingApiTests : IAsyncLifetime
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
    public async Task A_legacy_sale_request_keeps_retail_prices_and_zero_discounts()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-W1", "10.0000");
        await OpenAsync(client, product.Id, "4.0000");
        var response = await SendLegacyAsync(client, Guid.NewGuid(), "10.0000", (product.Id, "1.0000", "10.0000", "10.0000"));
        var sale = await ReadSaleAsync(response);

        Assert.Equal("0.0000", sale.DiscountAmount);
        Assert.Equal("none", sale.DiscountKind);
        Assert.Null(sale.DiscountRate);
        var line = Assert.Single(sale.Lines);
        Assert.Equal("retail", line.PriceKind);
        Assert.Equal("0.0000", line.LineDiscount);
        Assert.Equal("0.0000", line.AllocatedSaleDiscount);
        Assert.False(line.BelowMinimumOverride);
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var stored = await database.SaleLines.SingleAsync(item => item.Id == line.Id);
        Assert.Equal("retail", stored.PriceKind);
        Assert.Equal(0m, stored.LineDiscount);
        Assert.False(stored.BelowMinimumOverride);
    }

    [Fact]
    public async Task Percentage_and_fixed_discounts_allocate_and_do_not_change_stock_quantity()
    {
        using var client = await AdministratorClientAsync();
        var ten = await CreateProductAsync(client, "EAU-W2", "10.0000", minimum: "0.0000");
        var twenty = await CreateProductAsync(client, "EAU-W3", "20.0000", minimum: "0.0000");
        var thirty = await CreateProductAsync(client, "EAU-W4", "30.0000", minimum: "0.0000");
        await OpenAsync(client, ten.Id, "5.0000");
        await OpenAsync(client, twenty.Id, "1.0000");
        await OpenAsync(client, thirty.Id, "1.0000");
        var response = await SendPricedAsync(client, Guid.NewGuid(), new
        {
            total = "58.0000",
            discountAmount = "1.0000",
            discountKind = "amount",
            lines = new object[]
            {
                Line(ten.Id, "10.0000", "10.0000", "percent", "10.0000", "1.0000", "9.0000"),
                Line(twenty.Id, "20.0000", "20.0000", "none", null, "0.0000", "20.0000"),
                Line(thirty.Id, "30.0000", "30.0000", "none", null, "0.0000", "30.0000"),
            },
            payments = new[] { new { method = "cash", amount = "58.0000" } },
        });
        var sale = await ReadSaleAsync(response);

        Assert.Equal("58.0000", sale.Total);
        Assert.Equal(["0.1525", "0.3390", "0.5085"], sale.Lines.OrderBy(line => line.LineNumber).Select(line => line.AllocatedSaleDiscount));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(4.0000m, await database.StockBalances.Where(balance => balance.ProductId == ten.Id).Select(balance => balance.Quantity).SingleAsync());
        Assert.Equal(-1.0000m, await database.StockMovements.Where(movement => movement.ProductId == ten.Id && movement.SourceType == "sale").Select(movement => movement.QuantityDelta).SingleAsync());
    }

    [Fact]
    public async Task A_cashier_stops_at_the_floor_and_an_administrator_override_is_snapshotted()
    {
        using var admin = await AdministratorClientAsync();
        var product = await CreateProductAsync(admin, "EAU-W5", "10.0000", wholesale: "8.0000", minimum: "8.0000");
        await OpenAsync(admin, product.Id, "3.0000");
        await admin.PostAsJsonAsync("/api/v1/users", new { email = "cashier.price@example.com", password = "cashier-password-1" });
        using var cashier = await LoginAsync("cashier.price@example.com", "cashier-password-1");

        var atFloor = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(product.Id, "10.0000", "8.0000", "amount", null, "2.0000", "8.0000"));
        var below = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(product.Id, "10.0000", "7.0000", "amount", null, "3.0000", "7.0000"));
        var forged = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(product.Id, "1.0000", "1.0000", "none", null, "0.0000", "1.0000"));
        var wholesale = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(product.Id, "8.0000", "8.0000", "none", null, "0.0000", "8.0000", "wholesale"));
        var missingWholesale = await CreateProductAsync(admin, "EAU-W6", "10.0000");
        var noWholesale = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(missingWholesale.Id, "10.0000", "10.0000", "none", null, "0.0000", "10.0000", "wholesale"));
        var adminSale = await SendPricedAsync(admin, Guid.NewGuid(), Sale(product.Id, "6.0000", "6.0000", "none", null, "0.0000", "6.0000"));

        Assert.Equal(HttpStatusCode.Created, atFloor.StatusCode);
        Assert.False((await ReadSaleAsync(atFloor)).Lines[0].BelowMinimumOverride);
        Assert.Equal("discount.below_minimum", await FieldAsync(below));
        Assert.Equal("unitPrice.mismatch", await FieldAsync(forged));
        Assert.Equal("wholesale", (await ReadSaleAsync(wholesale)).Lines[0].PriceKind);
        Assert.Equal("price.wholesale_missing", await FieldAsync(noWholesale));
        var overridden = Assert.Single((await ReadSaleAsync(adminSale)).Lines);
        Assert.Equal("10.0000", overridden.ListUnitPrice);
        Assert.Equal("6.0000", overridden.UnitPrice);
        Assert.True(overridden.BelowMinimumOverride);
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(0m, await database.StockBalances.Where(balance => balance.ProductId == product.Id).Select(balance => balance.Quantity).SingleAsync());
        Assert.Equal(-3.0000m, await database.StockMovements.Where(movement => movement.ProductId == product.Id && movement.SourceType == "sale").SumAsync(movement => movement.QuantityDelta));
    }

    [Fact]
    public async Task The_same_priced_payload_replays_and_a_changed_discount_conflicts()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-W7", "10.0000", minimum: "0.0000");
        await OpenAsync(client, product.Id, "1.0000");
        var key = Guid.NewGuid();
        var body = Sale(product.Id, "10.0000", "9.0000", "percent", "10.0000", "1.0000", "9.0000");
        var first = await SendPricedAsync(client, key, body);
        var replay = await SendPricedAsync(client, key, body);
        var changed = await SendPricedAsync(client, key, Sale(product.Id, "10.0000", "8.0000", "amount", null, "2.0000", "8.0000"));

        Assert.Equal((await ReadSaleAsync(first)).Id, (await ReadSaleAsync(replay)).Id);
        Assert.Equal("sale.idempotency_conflict", await CodeAsync(changed));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(0m, await database.StockBalances.Where(balance => balance.ProductId == product.Id).Select(balance => balance.Quantity).SingleAsync());
        Assert.Equal(-1.0000m, await database.StockMovements.Where(movement => movement.ProductId == product.Id && movement.SourceType == "sale").SumAsync(movement => movement.QuantityDelta));
    }

    [Fact]
    public async Task A_cashier_accepts_a_fractional_quantity_when_the_floor_product_is_not_scale_4()
    {
        using var admin = await AdministratorClientAsync();
        var product = await CreateProductAsync(admin, "EAU-W9", "10.0000", minimum: "0.3333");
        var fractional = await CreateProductAsync(admin, "EAU-W10", "2.0000", minimum: "1.0001");
        var uneven = await CreateProductAsync(admin, "EAU-W11", "1.0001");
        await OpenAsync(admin, product.Id, "3.0000");
        await OpenAsync(admin, fractional.Id, "1.0001");
        await admin.PostAsJsonAsync("/api/v1/users", new { email = "cashier.floor@example.com", password = "cashier-password-1" });
        using var cashier = await LoginAsync("cashier.floor@example.com", "cashier-password-1");

        var above = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(product.Id, "10.0000", "15.0000", "none", null, "0.0000", "15.0000", quantity: "1.5000"));
        var atFloor = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(product.Id, "10.0000", "0.5000", "amount", null, "14.5000", "0.5000", quantity: "1.5000"));
        var below = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(product.Id, "10.0000", "0.4999", "amount", null, "14.5001", "0.4999", quantity: "1.5000"));
        var accepted = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(fractional.Id, "2.0000", "2.0002", "none", null, "0.0000", "2.0002", quantity: "1.0001"));
        var scale = await SendPricedAsync(cashier, Guid.NewGuid(), Sale(uneven.Id, "1.0001", "1.0002", "none", null, "0.0000", "1.0002", quantity: "1.0001"));

        Assert.Equal(HttpStatusCode.Created, above.StatusCode);
        Assert.False((await ReadSaleAsync(above)).Lines[0].BelowMinimumOverride);
        Assert.Equal(HttpStatusCode.Created, atFloor.StatusCode);
        Assert.False((await ReadSaleAsync(atFloor)).Lines[0].BelowMinimumOverride);
        Assert.Equal("discount.below_minimum", await FieldAsync(below));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal("line.scale", await FieldAsync(scale));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(0m, await database.StockBalances.Where(balance => balance.ProductId == product.Id).Select(balance => balance.Quantity).SingleAsync());
        Assert.Equal(-3.0000m, await database.StockMovements.Where(movement => movement.ProductId == product.Id && movement.SourceType == "sale").SumAsync(movement => movement.QuantityDelta));
        Assert.Equal(0m, await database.StockBalances.Where(balance => balance.ProductId == fractional.Id).Select(balance => balance.Quantity).SingleAsync());
        Assert.Equal(0, await database.StockBalances.CountAsync(balance => balance.ProductId == uneven.Id));
    }

    [Fact]
    public async Task Concurrent_updates_keep_the_wholesale_price_the_other_request_omits()
    {
        using var client = await AdministratorClientAsync();
        var created = await CreateProductAsync(client, "EAU-W12", "10.0000", wholesale: "8.0000", minimum: "7.0000");
        using var other = _factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;

        var responses = await Task.WhenAll(
            client.PatchAsJsonAsync($"/api/v1/products/{created.Id}", new
            {
                sku = "EAU-W12",
                name = "Eau",
                unit = "u",
                unitPrice = "10.0000",
                wholesaleUnitPrice = "7.5000",
                trackStock = true,
                isActive = true,
            }),
            other.PatchAsJsonAsync($"/api/v1/products/{created.Id}", new
            {
                sku = "EAU-W12",
                name = "Eau gazeuse",
                unit = "u",
                unitPrice = "10.0000",
                trackStock = true,
                isActive = true,
            }));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/v1/products", JsonOptions);
        var stored = products!.Single(product => product.Id == created.Id);
        Assert.Equal("7.5000", stored.WholesaleUnitPrice);
        Assert.Equal("7.0000", stored.MinimumUnitPrice);
        Assert.Equal("10.0000", stored.UnitPrice);
        Assert.True(stored.Name is "Eau" or "Eau gazeuse");
    }

    [Fact]
    public async Task Updating_a_product_preserves_prices_that_the_request_omits()
    {
        using var client = await AdministratorClientAsync();
        var created = await CreateProductAsync(client, "EAU-W8", "10.0000", wholesale: "8.0000", minimum: "7.0000");
        var renamed = await client.PatchAsJsonAsync($"/api/v1/products/{created.Id}", new
        {
            sku = "EAU-W8",
            name = "Eau plate",
            unit = "u",
            unitPrice = "10.0000",
            trackStock = true,
            isActive = true,
        });
        var cleared = await client.PatchAsJsonAsync($"/api/v1/products/{created.Id}", new
        {
            sku = "EAU-W8",
            name = "Eau plate",
            unit = "u",
            unitPrice = "10.0000",
            wholesaleUnitPrice = (string?)null,
            trackStock = true,
            isActive = true,
        });
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/v1/products", JsonOptions);
        var afterRename = await renamed.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        var afterClear = await cleared.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("8.0000", afterRename!.WholesaleUnitPrice);
        Assert.Equal("7.0000", afterRename.MinimumUnitPrice);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null(afterClear!.WholesaleUnitPrice);
        Assert.Equal("7.0000", afterClear.MinimumUnitPrice);
        Assert.Equal("Eau plate", products!.Single(product => product.Id == created.Id).Name);
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

    private static async Task<ProductResponse> CreateProductAsync(
        HttpClient client,
        string sku,
        string unitPrice,
        string? wholesale = null,
        string? minimum = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            sku,
            name = "Eau",
            unit = "u",
            unitPrice,
            wholesaleUnitPrice = wholesale,
            minimumUnitPrice = minimum,
            trackStock = true,
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
    }

    private static async Task OpenAsync(HttpClient client, Guid productId, string quantity)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/stock/opening")
        {
            Content = JsonContent.Create(new { productId, quantity, reason = "Ouverture" }),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("D"));
        var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static object Sale(
        Guid productId,
        string unitPrice,
        string total,
        string discountKind,
        string? discountRate,
        string lineDiscount,
        string lineTotal,
        string priceKind = "retail",
        string quantity = "1.0000") => new
    {
        total,
        discountAmount = "0.0000",
        discountKind = "none",
        lines = new[] { Line(productId, unitPrice, unitPrice, discountKind, discountRate, lineDiscount, lineTotal, priceKind, quantity) },
        payments = new[] { new { method = "cash", amount = total } },
    };

    private static object Line(
        Guid productId,
        string listPrice,
        string unitPrice,
        string discountKind,
        string? discountRate,
        string lineDiscount,
        string lineTotal,
        string priceKind = "retail",
        string quantity = "1.0000") => new
    {
        productId,
        quantity,
        priceKind,
        unitPrice,
        lineDiscount,
        discountKind,
        discountRate,
        lineTotal,
        listPrice,
    };

    private static Task<HttpResponseMessage> SendLegacyAsync(
        HttpClient client,
        Guid key,
        string total,
        params (Guid ProductId, string Quantity, string UnitPrice, string LineTotal)[] lines)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sales")
        {
            Content = JsonContent.Create(new
            {
                total,
                lines = lines.Select(line => new
                {
                    productId = line.ProductId,
                    quantity = line.Quantity,
                    unitPrice = line.UnitPrice,
                    lineTotal = line.LineTotal,
                }),
                payments = new[] { new { method = "cash", amount = total } },
            }),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));
        return client.SendAsync(message);
    }

    private static Task<HttpResponseMessage> SendPricedAsync(HttpClient client, Guid key, object body)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sales")
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));
        return client.SendAsync(message);
    }

    private static async Task<SaleResponse> ReadSaleAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SaleResponse>(JsonOptions))!;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ProblemBody>(JsonOptions);
        return body?.Code;
    }

    private static async Task<string?> FieldAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ProblemBody>(JsonOptions);
        return body?.Errors?.Values.FirstOrDefault()?.FirstOrDefault();
    }

    private sealed record ProblemBody(string? Code, Dictionary<string, string[]>? Errors);

    private sealed record TokenPair(string AccessToken, string RefreshToken);
}
