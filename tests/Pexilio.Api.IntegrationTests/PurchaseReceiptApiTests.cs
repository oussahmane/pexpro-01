using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pexilio.Application;
using Pexilio.Domain;
using Pexilio.Domain.Purchasing;
using Pexilio.Domain.Stock;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Api.IntegrationTests;

public sealed class PurchaseReceiptApiTests : IAsyncLifetime
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
    public async Task Ordering_freezes_the_draft_and_does_not_change_stock()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R1", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var draft = await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.5000");
        var ordered = await OrderAsync(client, draft);

        Assert.Equal(PurchaseStatuses.Ordered, ordered.Status);
        Assert.NotNull(ordered.OrderedAt);
        Assert.Equal("1.5000", ordered.Lines.Single().UnitCost);
        var edited = await client.PutAsJsonAsync($"/api/v1/purchases/{draft.Id}", new
        {
            supplierId = supplier.Id,
            version = ordered.Version,
            lines = new[] { new { productId = product.Id, quantity = "3.0000", unitCost = "1.5000" } },
        });
        var deleted = await client.DeleteAsync($"/api/v1/purchases/{draft.Id}?version={ordered.Version}");
        Assert.Equal(HttpStatusCode.Conflict, edited.StatusCode);
        Assert.Equal("purchase.not_draft", await CodeAsync(edited));
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Equal("purchase.not_draft", await CodeAsync(deleted));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(0, await database.StockMovements.CountAsync());
        Assert.Equal(0, await database.PurchaseReceipts.CountAsync());
    }

    [Fact]
    public async Task Empty_inactive_and_untracked_drafts_cannot_be_ordered()
    {
        using var client = await AdministratorClientAsync();
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var inactiveProduct = await CreateProductAsync(client, "EAU-R2", true, true);
        var untrackedProduct = await CreateProductAsync(client, "SRV-R1", true, true);
        var empty = await PostPurchaseAsync(client, supplier.Id, inactiveProduct.Id, "1.0000", "1.0000", includeLine: false);
        var inactiveDraft = await PostPurchaseAsync(client, supplier.Id, inactiveProduct.Id, "1.0000", "1.0000");
        var untrackedDraft = await PostPurchaseAsync(client, supplier.Id, untrackedProduct.Id, "1.0000", "1.0000");
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/products/{inactiveProduct.Id}", new
        {
            sku = "EAU-R2",
            name = "Eau",
            unit = "u",
            unitPrice = "10.0000",
            trackStock = true,
            isActive = false,
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/products/{untrackedProduct.Id}", new
        {
            sku = "SRV-R1",
            name = "Eau",
            unit = "u",
            unitPrice = "10.0000",
            trackStock = false,
            isActive = true,
        })).StatusCode);

        Assert.Equal("lines.required", await FieldAsync(await SendOrderAsync(client, empty, Guid.NewGuid())));
        Assert.Equal("purchase.product_inactive", await CodeAsync(await SendOrderAsync(client, inactiveDraft, Guid.NewGuid())));
        Assert.Equal("stock.not_tracked", await CodeAsync(await SendOrderAsync(client, untrackedDraft, Guid.NewGuid())));
    }

    [Fact]
    public async Task Partial_then_final_receipt_posts_one_movement_per_product()
    {
        using var client = await AdministratorClientAsync();
        var water = await CreateProductAsync(client, "EAU-R3", true, true);
        var flour = await CreateProductAsync(client, "FAR-R1", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var draft = await PostPurchaseAsync(client, supplier.Id, water.Id, "4.0000", "2.0000", flour.Id, "1.0000", "3.0000");
        var ordered = await OrderAsync(client, draft);
        var partial = await ReceiveAsync(client, ordered, "BL-1", (ordered.Lines[0].Id, "1.5000"));
        var reloaded = await client.GetFromJsonAsync<PurchaseResponse>($"/api/v1/purchases/{draft.Id}", JsonOptions);
        var final = await ReceiveAsync(client, reloaded!, "BL-2", (reloaded!.Lines[0].Id, "2.5000"), (reloaded.Lines[1].Id, "1.0000"));
        var done = await client.GetFromJsonAsync<PurchaseResponse>($"/api/v1/purchases/{draft.Id}", JsonOptions);

        Assert.StartsWith("R-", partial.Number);
        Assert.StartsWith("R-", final.Number);
        Assert.Equal(PurchaseStatuses.PartiallyReceived, reloaded.Status);
        Assert.Equal(PurchaseStatuses.Received, done!.Status);
        Assert.NotNull(done.ReceivedAt);
        Assert.Equal(2, done.Receipts.Count);
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var movements = await database.StockMovements.OrderBy(movement => movement.OccurredAtUtc).ToListAsync();
        Assert.Equal(3, movements.Count);
        Assert.All(movements, movement =>
        {
            Assert.Equal(StockMovementTypes.Receipt, movement.MovementType);
            Assert.Equal(StockSourceTypes.Purchase, movement.SourceType);
            Assert.NotNull(movement.SourceId);
        });
        Assert.Equal(4.0000m, await database.StockBalances.Where(balance => balance.ProductId == water.Id).Select(balance => balance.Quantity).SingleAsync());
        Assert.Equal(1.0000m, await database.StockBalances.Where(balance => balance.ProductId == flour.Id).Select(balance => balance.Quantity).SingleAsync());
        Assert.Equal(4.0000m, movements.Where(movement => movement.ProductId == water.Id).Sum(movement => movement.QuantityDelta));
        var lastWater = movements.Last(movement => movement.ProductId == water.Id);
        Assert.Equal(4.0000m, lastWater.BalanceAfter);
    }

    [Theory]
    [InlineData("0.0000")]
    [InlineData("-1.0000")]
    [InlineData("1.00000")]
    [InlineData("9.0000")]
    public async Task Invalid_receipt_quantities_post_nothing(string quantity)
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R4", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.0000"));
        var response = await SendReceiptAsync(client, ordered, "BL-1", Guid.NewGuid(), (ordered.Lines.Single().Id, quantity));

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(0, await database.StockMovements.CountAsync());
        Assert.Equal(0, await database.PurchaseReceipts.CountAsync());
        Assert.Equal(0m, await database.PurchaseLines.Select(line => line.QuantityReceived).SingleAsync());
    }

    [Fact]
    public async Task Receipt_replay_returns_the_stored_receipt_and_a_different_body_conflicts()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R5", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.0000"));
        var key = Guid.NewGuid();
        var first = await SendReceiptAsync(client, ordered, "BL-1", key, (ordered.Lines.Single().Id, "1.0000"));
        var replay = await SendReceiptAsync(client, ordered, "BL-1", key, (ordered.Lines.Single().Id, "1.0000"));
        var conflict = await SendReceiptAsync(client, ordered, "BL-1", key, (ordered.Lines.Single().Id, "2.0000"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("purchase.idempotency_conflict", await CodeAsync(conflict));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(1, await database.StockMovements.CountAsync());
    }

    [Fact]
    public async Task Cancel_before_receipt_succeeds_and_cancel_after_a_partial_receipt_does_not()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R6", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.0000"));
        var cancelled = await CancelAsync(client, ordered);
        Assert.Equal(PurchaseStatuses.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CancelledAt);
        var blocked = await SendReceiptAsync(client, cancelled, "BL-1", Guid.NewGuid(), (cancelled.Lines.Single().Id, "1.0000"));
        Assert.Equal("purchase.not_receivable", await CodeAsync(blocked));

        var second = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.0000"));
        await ReceiveAsync(client, second, "BL-2", (second.Lines.Single().Id, "1.0000"));
        var current = await client.GetFromJsonAsync<PurchaseResponse>($"/api/v1/purchases/{second.Id}", JsonOptions);
        var denied = await SendCancelAsync(client, current!, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Equal("purchase.not_cancellable", await CodeAsync(denied));
    }

    [Fact]
    public async Task Deactivating_a_product_or_supplier_after_ordering_still_allows_receipt()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R7", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "1.0000", "1.0000"));
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/products/{product.Id}", new
        {
            sku = "EAU-R7",
            name = "Eau",
            unit = "u",
            unitPrice = "10.0000",
            trackStock = true,
            isActive = false,
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/suppliers/{supplier.Id}", new { name = "Atlas", isActive = false })).StatusCode);
        var receipt = await ReceiveAsync(client, ordered, "BL-7", (ordered.Lines.Single().Id, "1.0000"));
        Assert.Equal("1.0000", receipt.Lines.Single().Quantity);
    }

    [Fact]
    public async Task Turning_stock_tracking_off_rejects_the_receipt()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R8", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "1.0000", "1.0000"));
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/products/{product.Id}", new
        {
            sku = "EAU-R8",
            name = "Eau",
            unit = "u",
            unitPrice = "10.0000",
            trackStock = false,
            isActive = true,
        })).StatusCode);
        var rejected = await SendReceiptAsync(client, ordered, "BL-8", Guid.NewGuid(), (ordered.Lines.Single().Id, "1.0000"));
        Assert.Equal("stock.not_tracked", await CodeAsync(rejected));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(0, await database.StockMovements.CountAsync());
    }

    [Fact]
    public async Task A_stale_version_and_a_cashier_cannot_receive()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R9", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.0000"));
        var stale = ordered with { Version = ordered.Version - 1 };
        var rejected = await SendReceiptAsync(client, stale, "BL-9", Guid.NewGuid(), (ordered.Lines.Single().Id, "1.0000"));
        Assert.Equal("purchase.conflict", await CodeAsync(rejected));

        await client.PostAsJsonAsync("/api/v1/users", new { email = "cashier.receipt@example.com", password = "cashier-password-1" });
        using var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = "cashier.receipt@example.com", password = "cashier-password-1" });
        var tokens = await login.Content.ReadFromJsonAsync<TokenPair>(JsonOptions);
        using var cashier = _factory.CreateClient();
        cashier.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var read = await cashier.GetAsync($"/api/v1/purchases/{ordered.Id}");
        var write = await SendReceiptAsync(cashier, ordered, "BL-9", Guid.NewGuid(), (ordered.Lines.Single().Id, "1.0000"));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Two_movements_for_one_product_in_one_transaction_roll_back_with_stock_reconciliation()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R10", true, true);
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
        var userId = await database.Users.Select(user => user.Id).SingleAsync();
        await using var tx = await database.Database.BeginTransactionAsync();
        var first = await stock.PostInCurrentTransactionAsync(
            StockMovementTypes.Receipt, product.Id, 5.0000m, "Premier", null, userId, StockSourceTypes.Purchase, Guid.NewGuid(), CancellationToken.None);
        var second = await stock.PostInCurrentTransactionAsync(
            StockMovementTypes.Receipt, product.Id, 3.0000m, "Second", null, userId, StockSourceTypes.Purchase, Guid.NewGuid(), CancellationToken.None);
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(5.0000m, first.Value!.BalanceAfter);
        Assert.Equal(8.0000m, second.Value!.BalanceAfter);
        var failure = await Record.ExceptionAsync(() => tx.CommitAsync());
        Assert.NotNull(failure);
        Assert.Contains("stock.reconciliation", failure.ToString(), StringComparison.Ordinal);

        await using var check = _factory.Services.CreateAsyncScope();
        var stored = check.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(0, await stored.StockMovements.CountAsync());
        Assert.Equal(0, await stored.StockBalances.CountAsync());
    }

    [Fact]
    public async Task Posted_receipts_reject_update_delete_and_truncate()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R11", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.5000"));
        await ReceiveAsync(client, ordered, "BL-11", (ordered.Lines.Single().Id, "1.0000"));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var receiptId = await database.PurchaseReceipts.Select(receipt => receipt.Id).SingleAsync();
        var lineId = await database.PurchaseReceiptLines.Select(line => line.Id).SingleAsync();

        var updateLine = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE pexilio.purchase_receipt_line SET "Quantity" = 2.0000 WHERE "Id" = {lineId}"""));
        var deleteLine = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM pexilio.purchase_receipt_line WHERE "Id" = {lineId}"""));
        var updateReceipt = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE pexilio.purchase_receipt SET "DocumentRef" = {"BL-CHANGED"} WHERE "Id" = {receiptId}"""));
        var deleteReceipt = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM pexilio.purchase_receipt WHERE "Id" = {receiptId}"""));
        var truncateLine = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync(
            """TRUNCATE TABLE pexilio.purchase_receipt_line"""));
        // PostgreSQL checks foreign keys before BEFORE TRUNCATE triggers. purchase_receipt_line
        // references purchase_receipt, so TRUNCATE of the header alone fails with SQLSTATE 0A000
        // and the append-only trigger is not reached. That check is the correct protection and
        // does not depend on CASCADE. The trigger still rejects the only form the foreign-key
        // check allows: truncating the header and its lines in one command.
        var truncateReceipt = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync(
            """TRUNCATE TABLE pexilio.purchase_receipt"""));
        var truncateReceiptAndLines = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync(
            """TRUNCATE TABLE pexilio.purchase_receipt, pexilio.purchase_receipt_line"""));

        Assert.Contains(ErrorCodes.PurchaseReceiptImmutable, updateLine.MessageText, StringComparison.Ordinal);
        Assert.Contains(ErrorCodes.PurchaseReceiptImmutable, deleteLine.MessageText, StringComparison.Ordinal);
        Assert.Contains(ErrorCodes.PurchaseReceiptImmutable, updateReceipt.MessageText, StringComparison.Ordinal);
        Assert.Contains(ErrorCodes.PurchaseReceiptImmutable, deleteReceipt.MessageText, StringComparison.Ordinal);
        Assert.Contains(ErrorCodes.PurchaseReceiptImmutable, truncateLine.MessageText, StringComparison.Ordinal);
        Assert.Equal("0A000", truncateReceipt.SqlState);
        Assert.Contains("cannot truncate a table referenced in a foreign key constraint", truncateReceipt.MessageText, StringComparison.Ordinal);
        Assert.Contains(ErrorCodes.PurchaseReceiptImmutable, truncateReceiptAndLines.MessageText, StringComparison.Ordinal);
        Assert.Equal(1, await database.PurchaseReceipts.CountAsync());
        Assert.Equal(1, await database.PurchaseReceiptLines.CountAsync());
        Assert.Equal(1.0000m, await database.PurchaseReceiptLines.Select(line => line.Quantity).SingleAsync());
    }

    [Fact]
    public async Task Purchase_line_insert_must_start_at_zero_received_quantity()
    {
        using var client = await AdministratorClientAsync();
        var orderedProduct = await CreateProductAsync(client, "EAU-R12", true, true);
        var extraProduct = await CreateProductAsync(client, "EAU-R12B", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, orderedProduct.Id, "2.0000", "1.0000"));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();

        var rejected = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO pexilio.purchase_line
            ("Id", "PurchaseId", "LineNumber", "ProductId", "QuantityOrdered", "QuantityReceived", "UnitCost", "ProductSku", "ProductName", "ProductUnit")
            VALUES ({Guid.CreateVersion7()}, {ordered.Id}, 2, {extraProduct.Id}, 1.0000, 1.0000, 1.0000, {"EAU-R12B"}, {"Eau"}, {"u"})
            """));
        Assert.Contains(ErrorCodes.PurchaseReceiptIntegrity, rejected.MessageText, StringComparison.Ordinal);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO pexilio.purchase_line
            ("Id", "PurchaseId", "LineNumber", "ProductId", "QuantityOrdered", "QuantityReceived", "UnitCost", "ProductSku", "ProductName", "ProductUnit")
            VALUES ({Guid.CreateVersion7()}, {ordered.Id}, 2, {extraProduct.Id}, 1.0000, 0.0000, 1.0000, {"EAU-R12B"}, {"Eau"}, {"u"})
            """);
        Assert.Equal(2, await database.PurchaseLines.CountAsync(line => line.PurchaseId == ordered.Id));
        Assert.Equal(0m, await database.PurchaseLines.Where(line => line.ProductId == extraProduct.Id).Select(line => line.QuantityReceived).SingleAsync());
    }

    [Fact]
    public async Task QuantityReceived_without_matching_receipts_cannot_commit()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R13", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.0000"));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var lineId = ordered.Lines.Single().Id;

        var rejected = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE pexilio.purchase_line SET "QuantityReceived" = 1.0000 WHERE "Id" = {lineId}"""));
        Assert.Contains(ErrorCodes.PurchaseReceiptIntegrity, rejected.MessageText, StringComparison.Ordinal);
        Assert.Equal(0m, await database.PurchaseLines.Select(line => line.QuantityReceived).SingleAsync());
    }

    [Fact]
    public async Task Mismatched_received_quantity_rolls_back_the_receipt_and_stock()
    {
        using var client = await AdministratorClientAsync();
        var product = await CreateProductAsync(client, "EAU-R14", true, true);
        var supplier = await CreateSupplierAsync(client, "Atlas");
        var ordered = await OrderAsync(client, await PostPurchaseAsync(client, supplier.Id, product.Id, "2.0000", "1.5000"));
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PexilioDbContext>();
        var stock = scope.ServiceProvider.GetRequiredService<IStockService>();
        var userId = await database.Users.Select(user => user.Id).SingleAsync();
        var purchase = await database.Purchases.SingleAsync();
        var line = await database.PurchaseLines.SingleAsync();
        var receiptId = Guid.CreateVersion7();
        var receiptLineId = Guid.CreateVersion7();

        await using var tx = await database.Database.BeginTransactionAsync();
        var posted = await stock.PostInCurrentTransactionAsync(
            StockMovementTypes.Receipt,
            line.ProductId,
            1.0000m,
            "Ecart",
            line.UnitCost,
            userId,
            StockSourceTypes.Purchase,
            receiptLineId,
            CancellationToken.None);
        Assert.True(posted.Succeeded);
        line.QuantityReceived = 2.0000m;
        database.PurchaseReceipts.Add(new PurchaseReceipt
        {
            Id = receiptId,
            CompanyId = purchase.CompanyId,
            PurchaseId = purchase.Id,
            Number = "R-900014",
            DocumentRef = "BL-ERR",
            DocumentKey = "BL-ERR",
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            ReceivedByUserId = userId,
            Lines =
            [
                new PurchaseReceiptLine
                {
                    Id = receiptLineId,
                    ReceiptId = receiptId,
                    PurchaseId = purchase.Id,
                    CompanyId = purchase.CompanyId,
                    PurchaseLineId = line.Id,
                    ProductId = line.ProductId,
                    Quantity = 1.0000m,
                    UnitCost = line.UnitCost,
                    StockMovementId = posted.Value!.Id,
                },
            ],
        });
        await database.SaveChangesAsync();
        var failure = await Record.ExceptionAsync(() => tx.CommitAsync());
        Assert.NotNull(failure);
        Assert.Contains(ErrorCodes.PurchaseReceiptIntegrity, failure.ToString(), StringComparison.Ordinal);

        await using var check = _factory.Services.CreateAsyncScope();
        var stored = check.ServiceProvider.GetRequiredService<PexilioDbContext>();
        Assert.Equal(0, await stored.PurchaseReceipts.CountAsync());
        Assert.Equal(0, await stored.PurchaseReceiptLines.CountAsync());
        Assert.Equal(0, await stored.StockMovements.CountAsync());
        Assert.Equal(0, await stored.StockBalances.CountAsync());
        Assert.Equal(0m, await stored.PurchaseLines.Select(item => item.QuantityReceived).SingleAsync());
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
        if (setup.StatusCode == HttpStatusCode.Conflict)
        {
            Assert.Equal("setup.already_complete", await CodeAsync(setup));
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "owner@example.com", password = "correct-horse-battery" });
        var tokens = await login.Content.ReadFromJsonAsync<TokenPair>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, string sku, bool isActive, bool trackStock)
    {
        var response = await client.PostAsJsonAsync("/api/v1/products", new
        {
            sku,
            name = "Eau",
            unit = "u",
            unitPrice = "10.0000",
            trackStock,
            isActive,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
    }

    private static async Task<SupplierResponse> CreateSupplierAsync(HttpClient client, string name)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/suppliers")
        {
            Content = JsonContent.Create(new { name, isActive = true }),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("D"));
        var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SupplierResponse>(JsonOptions))!;
    }

    private static async Task<PurchaseResponse> PostPurchaseAsync(
        HttpClient client,
        Guid supplierId,
        Guid productId,
        string quantity,
        string unitCost,
        Guid? secondProductId = null,
        string? secondQuantity = null,
        string? secondUnitCost = null,
        bool includeLine = true)
    {
        var lines = new List<object>();
        if (includeLine)
        {
            lines.Add(new { productId, quantity, unitCost });
        }

        if (secondProductId is Guid second)
        {
            lines.Add(new { productId = second, quantity = secondQuantity, unitCost = secondUnitCost });
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/purchases")
        {
            Content = JsonContent.Create(new { supplierId, lines }),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("D"));
        var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseResponse>(JsonOptions))!;
    }

    private static async Task<PurchaseResponse> OrderAsync(HttpClient client, PurchaseResponse purchase)
    {
        var response = await SendOrderAsync(client, purchase, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseResponse>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> SendOrderAsync(HttpClient client, PurchaseResponse purchase, Guid key)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/purchases/{purchase.Id}/order")
        {
            Content = JsonContent.Create(new { version = purchase.Version }),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));
        return client.SendAsync(message);
    }

    private static async Task<PurchaseReceiptResponse> ReceiveAsync(
        HttpClient client,
        PurchaseResponse purchase,
        string documentRef,
        params (Guid LineId, string Quantity)[] lines)
    {
        var response = await SendReceiptAsync(client, purchase, documentRef, Guid.NewGuid(), lines);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseReceiptResponse>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> SendReceiptAsync(
        HttpClient client,
        PurchaseResponse purchase,
        string documentRef,
        Guid key,
        params (Guid LineId, string Quantity)[] lines)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/purchases/{purchase.Id}/receipts")
        {
            Content = JsonContent.Create(new
            {
                version = purchase.Version,
                documentRef,
                lines = lines.Select(line => new { purchaseLineId = line.LineId, quantity = line.Quantity }),
            }),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));
        return client.SendAsync(message);
    }

    private static async Task<PurchaseResponse> CancelAsync(HttpClient client, PurchaseResponse purchase)
    {
        var response = await SendCancelAsync(client, purchase, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseResponse>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> SendCancelAsync(HttpClient client, PurchaseResponse purchase, Guid key)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/purchases/{purchase.Id}/cancel")
        {
            Content = JsonContent.Create(new { version = purchase.Version }),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));
        return client.SendAsync(message);
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
}
