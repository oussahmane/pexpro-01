using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Pexilio.Application;
using Pexilio.Domain;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Purchasing;
using Pexilio.Domain.Stock;

namespace Pexilio.Infrastructure.Purchasing;

public sealed partial class PurchasingService
{
    public async Task<Result<PurchaseResponse>> OrderPurchaseAsync(
        Guid id,
        PurchaseVersionRequest request,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.Version is null)
        {
            return Result<PurchaseResponse>.Fail(OperationFailure.Validation([new FieldError("version", "version.required")]));
        }

        var hash = Fingerprint(
            PurchaseOperations.PurchaseOrder,
            string.Join('\u001f', id.ToString("D"), request.Version.Value.ToString(CultureInfo.InvariantCulture)));
        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var claim = NewClaim(company.Id, PurchaseOperations.PurchaseOrder, idempotencyKey, hash);
        database.PurchaseIdempotencies.Add(claim);
        var claimed = await SaveClaimAsync(cancellationToken);
        if (claimed == ClaimSave.Conflict)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return await ReplayPurchaseOperationAsync(company.Id, PurchaseOperations.PurchaseOrder, idempotencyKey, hash, cancellationToken);
        }

        if (claimed == ClaimSave.Failed)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("The purchase order idempotency claim could not be stored.");
        }

        await LockPurchaseAsync(company.Id, id, cancellationToken);
        var purchase = await database.Purchases
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);
        if (purchase is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.PurchaseNotFound));
        }

        if (purchase.Status != PurchaseStatuses.Draft)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseNotDraft));
        }

        if (!PurchaseTransitions.CanOrder(purchase.Status, purchase.Lines.Count))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Validation([new FieldError("lines", "lines.required")]));
        }

        if (purchase.Version != request.Version.Value)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseConflict));
        }

        var supplier = await database.Suppliers.SingleOrDefaultAsync(
            item => item.Id == purchase.SupplierId && item.CompanyId == company.Id,
            cancellationToken);
        if (supplier is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.SupplierNotFound));
        }

        if (!supplier.IsActive)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.SupplierInactive));
        }

        var products = await LoadProductsAsync(company.Id, purchase.Lines.Select(line => line.ProductId), cancellationToken);
        foreach (var line in purchase.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                await tx.RollbackAsync(cancellationToken);
                return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.ProductNotFound));
            }

            if (!product.IsActive)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseProductInactive));
            }

            if (!product.TrackStock)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.StockNotTracked));
            }
        }

        purchase.Status = PurchaseStatuses.Ordered;
        purchase.OrderedAtUtc = time.GetUtcNow();
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await database.Entry(purchase).ReloadAsync(cancellationToken);
            var prices = await SellingPricesAsync(company.Id, purchase.Lines.Select(line => line.ProductId), cancellationToken);
            var response = ToPurchase(purchase, prices, []);
            claim.PurchaseId = purchase.Id;
            claim.ResponseJson = JsonSerializer.Serialize(response, JsonOptions);
            await database.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result<PurchaseResponse>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseConflict));
        }
        catch (DbUpdateException exception) when (IsUnique(exception))
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(UniqueFailure(exception));
        }
    }

    public async Task<Result<PurchaseReceiptResponse>> ReceivePurchaseAsync(
        Guid id,
        PurchaseReceiptRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request.Version is null)
        {
            return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Validation([new FieldError("version", "version.required")]));
        }

        var requested = request.Lines?.Select(line => (line.PurchaseLineId, line.Quantity)).ToArray();
        if (!PurchasingValidator.TryReceipt(request.DocumentRef, requested, out var receipt, out var errors))
        {
            return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Validation(errors));
        }

        var hash = Fingerprint(PurchaseOperations.PurchaseReceive, ReceiveCanonical(id, request.Version.Value, receipt));
        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var claim = NewClaim(company.Id, PurchaseOperations.PurchaseReceive, idempotencyKey, hash);
        database.PurchaseIdempotencies.Add(claim);
        var claimed = await SaveClaimAsync(cancellationToken);
        if (claimed == ClaimSave.Conflict)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return await ReplayReceiptAsync(company.Id, idempotencyKey, hash, cancellationToken);
        }

        if (claimed == ClaimSave.Failed)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("The purchase receipt idempotency claim could not be stored.");
        }

        await LockPurchaseAsync(company.Id, id, cancellationToken);
        var purchase = await database.Purchases
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);
        if (purchase is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(404, ErrorCodes.PurchaseNotFound));
        }

        if (!PurchaseTransitions.CanReceive(purchase.Status))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseNotReceivable));
        }

        if (purchase.Version != request.Version.Value)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseConflict));
        }

        var selected = new List<(PurchaseLine Line, decimal Quantity)>();
        foreach (var line in receipt.Lines)
        {
            var purchaseLine = purchase.Lines.SingleOrDefault(item => item.Id == line.PurchaseLineId);
            if (purchaseLine is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Validation([new FieldError("lines", "lines.unknown")]));
            }

            var remaining = purchaseLine.QuantityOrdered - purchaseLine.QuantityReceived;
            if (!PurchaseTransitions.WithinRemaining(line.Quantity, remaining))
            {
                await tx.RollbackAsync(cancellationToken);
                return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseReceiptExcess));
            }

            selected.Add((purchaseLine, line.Quantity));
        }

        if (!PurchaseTransitions.OneMovementPerProduct(selected.Select(item => item.Line.ProductId)))
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("A purchase receipt posts at most one movement per product.");
        }

        var products = await LoadProductsAsync(company.Id, selected.Select(item => item.Line.ProductId), cancellationToken);
        foreach (var item in selected)
        {
            if (!products.TryGetValue(item.Line.ProductId, out var product))
            {
                await tx.RollbackAsync(cancellationToken);
                return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(404, ErrorCodes.ProductNotFound));
            }

            if (!product.TrackStock)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, ErrorCodes.StockNotTracked));
            }
        }

        if (await database.PurchaseReceipts.AnyAsync(
                item => item.PurchaseId == purchase.Id && item.DocumentKey == receipt.DocumentKey,
                cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseReceiptDocumentTaken));
        }

        var receiptId = Guid.CreateVersion7();
        var number = await NextReceiptNumberAsync(cancellationToken);
        var reason = ReceiptReason(number);
        var staged = new List<PurchaseReceiptLine>(selected.Count);
        foreach (var item in selected.OrderBy(item => item.Line.ProductId).ThenBy(item => item.Line.LineNumber))
        {
            var receiptLineId = Guid.CreateVersion7();
            var posted = await stock.PostInCurrentTransactionAsync(
                StockMovementTypes.Receipt,
                item.Line.ProductId,
                item.Quantity,
                reason,
                item.Line.UnitCost,
                userId,
                StockSourceTypes.Purchase,
                receiptLineId,
                cancellationToken);
            if (posted.Error is not null || posted.Value is null)
            {
                database.ChangeTracker.Clear();
                await tx.RollbackAsync(cancellationToken);
                return Result<PurchaseReceiptResponse>.Fail(posted.Error ?? OperationFailure.Of(409, ErrorCodes.StockReconciliation));
            }

            item.Line.QuantityReceived += item.Quantity;
            staged.Add(new PurchaseReceiptLine
            {
                Id = receiptLineId,
                ReceiptId = receiptId,
                PurchaseId = purchase.Id,
                CompanyId = company.Id,
                PurchaseLineId = item.Line.Id,
                ProductId = item.Line.ProductId,
                Quantity = item.Quantity,
                UnitCost = item.Line.UnitCost,
                StockMovementId = posted.Value.Id,
            });
        }

        var fullyReceived = purchase.Lines.All(line => line.QuantityReceived == line.QuantityOrdered);
        purchase.Status = PurchaseTransitions.AfterReceipt(fullyReceived);
        var receivedAt = time.GetUtcNow();
        if (fullyReceived)
        {
            purchase.ReceivedAtUtc = receivedAt;
        }

        var storedReceipt = new PurchaseReceipt
        {
            Id = receiptId,
            CompanyId = company.Id,
            PurchaseId = purchase.Id,
            Number = number,
            DocumentRef = receipt.DocumentRef,
            DocumentKey = receipt.DocumentKey,
            ReceivedAtUtc = receivedAt,
            ReceivedByUserId = userId,
            Lines = staged,
        };
        database.PurchaseReceipts.Add(storedReceipt);
        var response = ToReceipt(storedReceipt);
        claim.PurchaseId = purchase.Id;
        claim.ResponseJson = JsonSerializer.Serialize(response, JsonOptions);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result<PurchaseReceiptResponse>.Success(response);
        }
        catch (Exception exception) when (exception is DbUpdateException or PostgresException)
        {
            database.ChangeTracker.Clear();
            if (database.Database.CurrentTransaction is not null)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            if (IsUnique(exception))
            {
                return Result<PurchaseReceiptResponse>.Fail(UniqueFailure(exception));
            }

            if (Mentions(exception, ErrorCodes.PurchaseReceiptIntegrity) || Mentions(exception, ErrorCodes.StockReconciliation))
            {
                var code = Mentions(exception, ErrorCodes.PurchaseReceiptIntegrity)
                    ? ErrorCodes.PurchaseReceiptIntegrity
                    : ErrorCodes.StockReconciliation;
                return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, code));
            }

            if (Mentions(exception, ErrorCodes.StockInsufficient))
            {
                return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, ErrorCodes.StockInsufficient));
            }

            throw;
        }
    }

    public async Task<Result<PurchaseResponse>> CancelPurchaseAsync(
        Guid id,
        PurchaseVersionRequest request,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.Version is null)
        {
            return Result<PurchaseResponse>.Fail(OperationFailure.Validation([new FieldError("version", "version.required")]));
        }

        var hash = Fingerprint(
            PurchaseOperations.PurchaseCancel,
            string.Join('\u001f', id.ToString("D"), request.Version.Value.ToString(CultureInfo.InvariantCulture)));
        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var claim = NewClaim(company.Id, PurchaseOperations.PurchaseCancel, idempotencyKey, hash);
        database.PurchaseIdempotencies.Add(claim);
        var claimed = await SaveClaimAsync(cancellationToken);
        if (claimed == ClaimSave.Conflict)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return await ReplayPurchaseOperationAsync(company.Id, PurchaseOperations.PurchaseCancel, idempotencyKey, hash, cancellationToken);
        }

        if (claimed == ClaimSave.Failed)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("The purchase cancellation idempotency claim could not be stored.");
        }

        await LockPurchaseAsync(company.Id, id, cancellationToken);
        var purchase = await database.Purchases
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);
        if (purchase is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.PurchaseNotFound));
        }

        var anyReceived = purchase.Lines.Any(line => line.QuantityReceived > 0m);
        var anyReceipt = await database.PurchaseReceipts.AnyAsync(
            item => item.CompanyId == company.Id && item.PurchaseId == purchase.Id,
            cancellationToken);
        if (!PurchaseTransitions.CanCancel(purchase.Status, anyReceived, anyReceipt))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseNotCancellable));
        }

        if (purchase.Version != request.Version.Value)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseConflict));
        }

        purchase.Status = PurchaseStatuses.Cancelled;
        purchase.CancelledAtUtc = time.GetUtcNow();
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await database.Entry(purchase).ReloadAsync(cancellationToken);
            var prices = await SellingPricesAsync(company.Id, purchase.Lines.Select(line => line.ProductId), cancellationToken);
            var response = ToPurchase(purchase, prices, []);
            claim.PurchaseId = purchase.Id;
            claim.ResponseJson = JsonSerializer.Serialize(response, JsonOptions);
            await database.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result<PurchaseResponse>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseConflict));
        }
    }

    private async Task<Result<PurchaseResponse>> ReplayPurchaseOperationAsync(
        Guid companyId,
        string operation,
        Guid idempotencyKey,
        string hash,
        CancellationToken cancellationToken)
    {
        var claim = await FindClaimAsync(companyId, operation, idempotencyKey, cancellationToken);
        var sameRequest = claim is not null && claim.RequestHash == hash;
        var hasStoredResponse = claim is not null && !string.IsNullOrEmpty(claim.ResponseJson);
        var purchaseExists = false;
        if (sameRequest && hasStoredResponse && claim!.PurchaseId is Guid purchaseId)
        {
            purchaseExists = await database.Purchases.AsNoTracking().AnyAsync(
                item => item.Id == purchaseId && item.CompanyId == companyId,
                cancellationToken);
        }

        switch (PurchaseReplay.Decide(sameRequest, hasStoredResponse, purchaseExists))
        {
            case PurchaseReplayOutcome.ReturnStored:
                var stored = JsonSerializer.Deserialize<PurchaseResponse>(claim!.ResponseJson, JsonOptions);
                return stored is null
                    ? Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseIdempotencyConflict))
                    : Result<PurchaseResponse>.Success(stored);
            case PurchaseReplayOutcome.Missing:
                return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.PurchaseNotFound));
            default:
                return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseIdempotencyConflict));
        }
    }

    private async Task<Result<PurchaseReceiptResponse>> ReplayReceiptAsync(
        Guid companyId,
        Guid idempotencyKey,
        string hash,
        CancellationToken cancellationToken)
    {
        var claim = await FindClaimAsync(companyId, PurchaseOperations.PurchaseReceive, idempotencyKey, cancellationToken);
        var sameRequest = claim is not null && claim.RequestHash == hash;
        var hasStoredResponse = claim is not null && !string.IsNullOrEmpty(claim.ResponseJson);
        var purchaseExists = false;
        if (sameRequest && hasStoredResponse && claim!.PurchaseId is Guid purchaseId)
        {
            purchaseExists = await database.Purchases.AsNoTracking().AnyAsync(
                item => item.Id == purchaseId && item.CompanyId == companyId,
                cancellationToken);
        }

        switch (PurchaseReplay.Decide(sameRequest, hasStoredResponse, purchaseExists))
        {
            case PurchaseReplayOutcome.ReturnStored:
                var stored = JsonSerializer.Deserialize<PurchaseReceiptResponse>(claim!.ResponseJson, JsonOptions);
                return stored is null
                    ? Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseIdempotencyConflict))
                    : Result<PurchaseReceiptResponse>.Success(stored);
            case PurchaseReplayOutcome.Missing:
                return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(404, ErrorCodes.PurchaseNotFound));
            default:
                return Result<PurchaseReceiptResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseIdempotencyConflict));
        }
    }

    private async Task<Dictionary<Guid, Product>> LoadProductsAsync(
        Guid companyId,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        return await database.Products
            .Where(product => product.CompanyId == companyId && ids.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
    }

    private async Task<string> NextReceiptNumberAsync(CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT nextval('pexilio.purchase_receipt_number_seq')";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return ReceiptNumbers.Format(Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }

    private static string ReceiptReason(string number)
    {
        var reason = "Réception " + number;
        return reason.Length <= StockRules.ReasonMaxLength ? reason : reason[..StockRules.ReasonMaxLength];
    }

    private static string ReceiveCanonical(Guid purchaseId, uint version, ReceiptInput receipt)
    {
        var parts = new List<string>
        {
            purchaseId.ToString("D"),
            version.ToString(CultureInfo.InvariantCulture),
            receipt.DocumentKey,
        };
        foreach (var line in receipt.Lines)
        {
            parts.Add(line.PurchaseLineId.ToString("D"));
            parts.Add(QuantityText.Format(line.Quantity));
        }

        return string.Join('\u001f', parts);
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
}
