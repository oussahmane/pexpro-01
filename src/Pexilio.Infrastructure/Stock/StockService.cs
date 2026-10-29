using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pexilio.Application;
using Pexilio.Domain;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Stock;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Infrastructure.Stock;

public sealed class StockService(PexilioDbContext database, TimeProvider time) : IStockService
{
    private const long DefaultLocationLock = 481271002;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<StockLocationResponse>>> ListLocationsAsync(CancellationToken cancellationToken)
    {
        var company = await database.Companies.SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return Result<IReadOnlyList<StockLocationResponse>>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var location = await EnsureDefaultLocationAsync(company.Id, cancellationToken);
        return Result<IReadOnlyList<StockLocationResponse>>.Success([ToLocation(location)]);
    }

    public async Task<Result<IReadOnlyList<StockBalanceResponse>>> ListBalancesAsync(CancellationToken cancellationToken)
    {
        var company = await database.Companies.SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return Result<IReadOnlyList<StockBalanceResponse>>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var location = await EnsureDefaultLocationAsync(company.Id, cancellationToken);
        var products = await database.Products
            .Where(product => product.CompanyId == company.Id && product.TrackStock)
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Sku)
            .ToListAsync(cancellationToken);
        var balances = await database.StockBalances
            .Where(balance => balance.StockLocationId == location.Id)
            .ToListAsync(cancellationToken);
        var byProduct = balances.ToDictionary(balance => balance.ProductId);
        var rows = products.Select(product =>
        {
            byProduct.TryGetValue(product.Id, out var balance);
            return ToBalance(product, balance?.Quantity ?? 0m, balance?.MinimumQuantity);
        }).ToList();
        return Result<IReadOnlyList<StockBalanceResponse>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<StockMovementResponse>>> ListMovementsAsync(
        Guid? productId,
        CancellationToken cancellationToken)
    {
        var company = await database.Companies.SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return Result<IReadOnlyList<StockMovementResponse>>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var query = database.StockMovements.Where(movement => movement.CompanyId == company.Id);
        if (productId is not null)
        {
            query = query.Where(movement => movement.ProductId == productId);
        }

        var rows = await query
            .OrderByDescending(movement => movement.OccurredAtUtc)
            .ThenByDescending(movement => movement.Id)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<StockMovementResponse>>.Success(rows.Select(ToMovement).ToList());
    }

    public Task<Result<StockMovementResponse>> OpenAsync(
        StockQuantityRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken) =>
        PostAsync(StockMovementTypes.Opening, request, idempotencyKey, userId, cancellationToken);

    public Task<Result<StockMovementResponse>> ReceiveAsync(
        StockQuantityRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken) =>
        PostAsync(StockMovementTypes.Receipt, request, idempotencyKey, userId, cancellationToken);

    public Task<Result<StockMovementResponse>> AdjustAsync(
        StockQuantityRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken) =>
        PostAsync(StockMovementTypes.Adjustment, request, idempotencyKey, userId, cancellationToken);

    public async Task<Result<StockBalanceResponse>> SetMinimumAsync(
        Guid productId,
        MinimumStockRequest request,
        CancellationToken cancellationToken)
    {
        decimal? minimum = null;
        if (!string.IsNullOrWhiteSpace(request.MinimumQuantity))
        {
            if (!QuantityText.TryParse(request.MinimumQuantity, "minimumQuantity", false, out var parsed, out var code))
            {
                return Result<StockBalanceResponse>.Fail(OperationFailure.Validation([new FieldError("minimumQuantity", code)]));
            }

            minimum = parsed;
        }

        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await database.Companies.SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<StockBalanceResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var product = await database.Products.SingleOrDefaultAsync(
            item => item.Id == productId && item.CompanyId == company.Id,
            cancellationToken);
        if (product is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<StockBalanceResponse>.Fail(OperationFailure.Of(404, ErrorCodes.ProductNotFound));
        }

        if (!product.TrackStock)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<StockBalanceResponse>.Fail(OperationFailure.Of(409, ErrorCodes.StockNotTracked));
        }

        var location = await EnsureDefaultLocationAsync(company.Id, cancellationToken);
        await LockProductAsync(location.Id, product.Id, cancellationToken);
        var balance = await EnsureBalanceAsync(location.Id, product.Id, cancellationToken);
        await database.StockBalances
            .Where(item => item.Id == balance.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.MinimumQuantity, minimum),
                cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result<StockBalanceResponse>.Success(ToBalance(product, balance.Quantity, minimum));
    }

    private async Task<Result<StockMovementResponse>> PostAsync(
        string movementType,
        StockQuantityRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!StockRules.TryReason(request.Reason, out var reason, out var reasonCode))
        {
            return Result<StockMovementResponse>.Fail(OperationFailure.Validation([new FieldError("reason", reasonCode)]));
        }

        var allowNegative = movementType == StockMovementTypes.Adjustment;
        if (!QuantityText.TryParse(request.Quantity, "quantity", allowNegative, out var quantity, out var quantityCode)
            || !StockRules.TryDelta(movementType, quantity, out quantityCode))
        {
            return Result<StockMovementResponse>.Fail(OperationFailure.Validation([new FieldError("quantity", quantityCode)]));
        }

        decimal? unitCost = null;
        if (movementType == StockMovementTypes.Receipt && !string.IsNullOrWhiteSpace(request.UnitCost))
        {
            if (!QuantityText.TryParse(request.UnitCost, "unitCost", false, out var parsedCost, out var costCode))
            {
                return Result<StockMovementResponse>.Fail(OperationFailure.Validation([new FieldError("unitCost", costCode)]));
            }

            unitCost = parsedCost;
        }

        var hash = Fingerprint(movementType, request.ProductId, quantity, reason, unitCost);
        var attempt = await TryPostAsync(
            movementType,
            request.ProductId,
            quantity,
            reason,
            unitCost,
            idempotencyKey,
            userId,
            hash,
            cancellationToken);
        if (attempt.ReplayCompanyId is Guid companyId)
        {
            return await ReplayAsync(companyId, idempotencyKey, hash, cancellationToken);
        }

        return attempt.Error is null
            ? Result<StockMovementResponse>.Success(attempt.Response!)
            : Result<StockMovementResponse>.Fail(attempt.Error);
    }

    private async Task<PostAttempt> TryPostAsync(
        string movementType,
        Guid productId,
        decimal quantity,
        string reason,
        decimal? unitCost,
        Guid idempotencyKey,
        Guid userId,
        string hash,
        CancellationToken cancellationToken)
    {
        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await database.Companies.SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return PostAttempt.Failed(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var product = await database.Products.SingleOrDefaultAsync(
            item => item.Id == productId && item.CompanyId == company.Id,
            cancellationToken);
        if (product is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return PostAttempt.Failed(OperationFailure.Of(404, ErrorCodes.ProductNotFound));
        }

        if (!product.TrackStock)
        {
            await tx.RollbackAsync(cancellationToken);
            return PostAttempt.Failed(OperationFailure.Of(409, ErrorCodes.StockNotTracked));
        }

        database.StockIdempotencies.Add(new StockIdempotency
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            IdempotencyKey = idempotencyKey,
            RequestHash = hash,
            ResponseJson = "",
            CreatedAtUtc = time.GetUtcNow(),
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return PostAttempt.Replay(company.Id);
        }

        var location = await EnsureDefaultLocationAsync(company.Id, cancellationToken);
        await LockProductAsync(location.Id, product.Id, cancellationToken);
        var balance = await EnsureBalanceAsync(location.Id, product.Id, cancellationToken);

        if (movementType == StockMovementTypes.Opening)
        {
            var exists = await database.StockMovements.AnyAsync(
                movement => movement.StockLocationId == location.Id && movement.ProductId == product.Id,
                cancellationToken);
            if (exists)
            {
                database.ChangeTracker.Clear();
                await tx.RollbackAsync(cancellationToken);
                return PostAttempt.Failed(OperationFailure.Of(409, ErrorCodes.StockOpeningExists));
            }
        }

        var available = balance.Quantity;
        var next = available + quantity;
        if (location.PreventNegativeStock && next < 0m)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return PostAttempt.Failed(Insufficient(available, quantity));
        }

        balance.Quantity = next;
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            StockLocationId = location.Id,
            ProductId = product.Id,
            MovementType = movementType,
            QuantityDelta = quantity,
            Reason = reason,
            BalanceAfter = next,
            UnitCost = unitCost,
            SourceType = StockMovementTypes.Manual,
            OccurredAtUtc = time.GetUtcNow(),
            CreatedByUserId = userId,
        };
        database.StockMovements.Add(movement);
        var response = ToMovement(movement);
        var claim = database.ChangeTracker.Entries<StockIdempotency>().Single().Entity;
        claim.MovementId = movement.Id;
        claim.ResponseJson = JsonSerializer.Serialize(response, JsonOptions);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return PostAttempt.Succeeded(response);
        }
        catch (Exception exception) when (exception is DbUpdateException or PostgresException)
        {
            database.ChangeTracker.Clear();
            if (database.Database.CurrentTransaction is not null)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            if (Mentions(exception, ErrorCodes.StockInsufficient))
            {
                return PostAttempt.Failed(Insufficient(available, quantity));
            }

            if (Mentions(exception, ErrorCodes.StockReconciliation))
            {
                return PostAttempt.Failed(OperationFailure.Of(409, ErrorCodes.StockReconciliation));
            }

            throw;
        }
    }

    private async Task<Result<StockMovementResponse>> ReplayAsync(
        Guid companyId,
        Guid idempotencyKey,
        string hash,
        CancellationToken cancellationToken)
    {
        var claim = await database.StockIdempotencies
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CompanyId == companyId && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (claim is null || claim.RequestHash != hash || string.IsNullOrEmpty(claim.ResponseJson))
        {
            return Result<StockMovementResponse>.Fail(OperationFailure.Of(409, ErrorCodes.StockIdempotencyConflict));
        }

        var stored = JsonSerializer.Deserialize<StockMovementResponse>(claim.ResponseJson, JsonOptions);
        return stored is null
            ? Result<StockMovementResponse>.Fail(OperationFailure.Of(409, ErrorCodes.StockIdempotencyConflict))
            : Result<StockMovementResponse>.Success(stored);
    }

    private async Task<StockLocation> EnsureDefaultLocationAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var existing = await FindDefaultLocationAsync(companyId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (database.Database.CurrentTransaction is null)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var created = await CreateDefaultLocationAsync(companyId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return created;
        }

        return await CreateDefaultLocationAsync(companyId, cancellationToken);
    }

    private async Task<StockLocation> CreateDefaultLocationAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({DefaultLocationLock})",
            cancellationToken);
        var existing = await FindDefaultLocationAsync(companyId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var id = Guid.NewGuid();
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO pexilio.stock_location
                ("Id", "CompanyId", "Code", "Name", "IsDefault", "PreventNegativeStock")
            VALUES
                ({id}, {companyId}, {StockRules.DefaultLocationCode}, {StockRules.DefaultLocationName}, TRUE, TRUE)
            ON CONFLICT ("CompanyId") WHERE "IsDefault" DO NOTHING
            """,
            cancellationToken);
        return await database.StockLocations.SingleAsync(
            location => location.CompanyId == companyId && location.IsDefault,
            cancellationToken);
    }

    private Task<StockLocation?> FindDefaultLocationAsync(Guid companyId, CancellationToken cancellationToken) =>
        database.StockLocations.SingleOrDefaultAsync(
            location => location.CompanyId == companyId && location.IsDefault,
            cancellationToken);

    private Task LockProductAsync(Guid locationId, Guid productId, CancellationToken cancellationToken)
    {
        var locationKey = locationId.ToString("D");
        var productKey = productId.ToString("D");
        return database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({locationKey}), hashtext({productKey}))",
            cancellationToken);
    }

    private async Task<StockBalance> EnsureBalanceAsync(Guid locationId, Guid productId, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO pexilio.stock_balance ("Id", "StockLocationId", "ProductId", "Quantity")
            VALUES ({id}, {locationId}, {productId}, 0)
            ON CONFLICT ("StockLocationId", "ProductId") DO NOTHING
            """,
            cancellationToken);
        return await database.StockBalances.SingleAsync(
            balance => balance.StockLocationId == locationId && balance.ProductId == productId,
            cancellationToken);
    }

    private static string Fingerprint(
        string movementType,
        Guid productId,
        decimal quantity,
        string reason,
        decimal? unitCost)
    {
        var unitCostText = unitCost is null ? "" : QuantityText.Format(unitCost.Value);
        var canonical = string.Join(
            '\u001f',
            movementType,
            productId.ToString("D"),
            QuantityText.Format(quantity),
            reason,
            unitCostText);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static OperationFailure Insufficient(decimal available, decimal requested) =>
        OperationFailure.Of(
            409,
            ErrorCodes.StockInsufficient,
            new Dictionary<string, string>
            {
                ["available"] = QuantityText.Format(available),
                ["requested"] = QuantityText.Format(requested),
            });

    private static StockLocationResponse ToLocation(StockLocation location) =>
        new(location.Id, location.Code, location.Name, location.IsDefault, location.PreventNegativeStock);

    private static StockBalanceResponse ToBalance(Product product, decimal quantity, decimal? minimum) =>
        new(
            product.Id,
            product.Sku,
            product.Name,
            product.Unit,
            QuantityText.Format(quantity),
            minimum is null ? null : QuantityText.Format(minimum.Value),
            StockRules.BelowMinimum(quantity, minimum),
            StockRules.AtMinimum(quantity, minimum));

    private static StockMovementResponse ToMovement(StockMovement movement) =>
        new(
            movement.Id,
            movement.ProductId,
            movement.StockLocationId,
            movement.MovementType,
            QuantityText.Format(movement.QuantityDelta),
            movement.Reason,
            QuantityText.Format(movement.BalanceAfter),
            movement.OccurredAtUtc,
            movement.CreatedByUserId);

    private static bool IsIdempotencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && postgres.ConstraintName is not null
                && postgres.ConstraintName.Contains("idempotency", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Mentions(Exception exception, string token)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record PostAttempt(StockMovementResponse? Response, OperationFailure? Error, Guid? ReplayCompanyId)
    {
        public static PostAttempt Succeeded(StockMovementResponse response) => new(response, null, null);

        public static PostAttempt Failed(OperationFailure error) => new(null, error, null);

        public static PostAttempt Replay(Guid companyId) => new(null, null, companyId);
    }
}
