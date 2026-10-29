using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Pexilio.Application;
using Pexilio.Domain;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Company;
using Pexilio.Domain.Purchasing;
using Pexilio.Domain.Stock;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Infrastructure.Purchasing;

public sealed class PurchasingService(PexilioDbContext database, TimeProvider time) : IPurchasingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<SupplierResponse>>> ListSuppliersAsync(CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<IReadOnlyList<SupplierResponse>>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var rows = await database.Suppliers
            .AsNoTracking()
            .Where(supplier => supplier.CompanyId == company.Id)
            .OrderBy(supplier => supplier.Name)
            .ThenBy(supplier => supplier.Id)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<SupplierResponse>>.Success(rows.Select(ToSupplier).ToList());
    }

    public async Task<Result<SupplierResponse>> GetSupplierAsync(Guid id, CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<SupplierResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var supplier = await database.Suppliers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);
        return supplier is null
            ? Result<SupplierResponse>.Fail(OperationFailure.Of(404, ErrorCodes.SupplierNotFound))
            : Result<SupplierResponse>.Success(ToSupplier(supplier));
    }

    public async Task<Result<SupplierResponse>> CreateSupplierAsync(
        SupplierRequest request,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!PurchasingValidator.TrySupplier(
                request.Name,
                request.ContactName,
                request.Phone,
                request.Email,
                request.Address,
                request.TaxId,
                request.Notes,
                request.IsActive,
                out var draft,
                out var errors))
        {
            return Result<SupplierResponse>.Fail(OperationFailure.Validation(errors));
        }

        var hash = Fingerprint(PurchaseOperations.SupplierCreate, SupplierCanonical(draft));
        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<SupplierResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var claim = NewClaim(company.Id, PurchaseOperations.SupplierCreate, idempotencyKey, hash);
        database.PurchaseIdempotencies.Add(claim);
        var claimed = await SaveClaimAsync(cancellationToken);
        if (claimed == ClaimSave.Conflict)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return await ReplaySupplierAsync(company.Id, idempotencyKey, hash, cancellationToken);
        }

        if (claimed == ClaimSave.Failed)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("The supplier idempotency claim could not be stored.");
        }

        if (await SupplierNameTakenAsync(company.Id, draft.NameKey, null, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<SupplierResponse>.Fail(OperationFailure.Of(409, ErrorCodes.SupplierNameTaken));
        }

        var supplier = new Supplier
        {
            Id = Guid.CreateVersion7(),
            CompanyId = company.Id,
            Name = draft.Name,
            NameKey = draft.NameKey,
            ContactName = draft.ContactName,
            Phone = draft.Phone,
            Email = draft.Email,
            Address = draft.Address,
            TaxId = draft.TaxId,
            Notes = draft.Notes,
            IsActive = draft.IsActive,
            CreatedAtUtc = time.GetUtcNow(),
        };
        database.Suppliers.Add(supplier);
        var response = ToSupplier(supplier);
        claim.ResponseJson = JsonSerializer.Serialize(response, JsonOptions);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result<SupplierResponse>.Success(response);
        }
        catch (DbUpdateException exception) when (IsUnique(exception))
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return Result<SupplierResponse>.Fail(UniqueFailure(exception));
        }
    }

    public async Task<Result<SupplierResponse>> UpdateSupplierAsync(
        Guid id,
        SupplierRequest request,
        CancellationToken cancellationToken)
    {
        if (!PurchasingValidator.TrySupplier(
                request.Name,
                request.ContactName,
                request.Phone,
                request.Email,
                request.Address,
                request.TaxId,
                request.Notes,
                request.IsActive,
                out var draft,
                out var errors))
        {
            return Result<SupplierResponse>.Fail(OperationFailure.Validation(errors));
        }

        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<SupplierResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var supplier = await database.Suppliers.SingleOrDefaultAsync(
            item => item.Id == id && item.CompanyId == company.Id,
            cancellationToken);
        if (supplier is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<SupplierResponse>.Fail(OperationFailure.Of(404, ErrorCodes.SupplierNotFound));
        }

        if (await SupplierNameTakenAsync(company.Id, draft.NameKey, supplier.Id, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<SupplierResponse>.Fail(OperationFailure.Of(409, ErrorCodes.SupplierNameTaken));
        }

        supplier.Name = draft.Name;
        supplier.NameKey = draft.NameKey;
        supplier.ContactName = draft.ContactName;
        supplier.Phone = draft.Phone;
        supplier.Email = draft.Email;
        supplier.Address = draft.Address;
        supplier.TaxId = draft.TaxId;
        supplier.Notes = draft.Notes;
        supplier.IsActive = draft.IsActive;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result<SupplierResponse>.Success(ToSupplier(supplier));
        }
        catch (DbUpdateException exception) when (IsUnique(exception))
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return Result<SupplierResponse>.Fail(UniqueFailure(exception));
        }
    }

    public async Task<Result<IReadOnlyList<PurchaseSummaryResponse>>> ListPurchasesAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<IReadOnlyList<PurchaseSummaryResponse>>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var filter = status?.Trim();
        if (!string.IsNullOrEmpty(filter) && !PurchaseStatuses.All.Contains(filter))
        {
            return Result<IReadOnlyList<PurchaseSummaryResponse>>.Fail(
                OperationFailure.Validation([new FieldError("status", "status.invalid")]));
        }

        var query = database.Purchases.AsNoTracking().Where(purchase => purchase.CompanyId == company.Id);
        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(purchase => purchase.Status == filter);
        }

        var rows = await query
            .OrderByDescending(purchase => purchase.CreatedAtUtc)
            .ThenByDescending(purchase => purchase.Number)
            .Select(purchase => new PurchaseSummaryResponse(
                purchase.Id,
                purchase.Number,
                purchase.Status,
                purchase.SupplierId,
                purchase.SupplierName,
                purchase.SupplierDocumentRef,
                purchase.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PurchaseSummaryResponse>>.Success(rows);
    }

    public async Task<Result<PurchaseResponse>> GetPurchaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var purchase = await database.Purchases
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);
        if (purchase is null)
        {
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.PurchaseNotFound));
        }

        var prices = await SellingPricesAsync(company.Id, purchase.Lines.Select(line => line.ProductId), cancellationToken);
        return Result<PurchaseResponse>.Success(ToPurchase(purchase, prices));
    }

    public async Task<Result<PurchaseResponse>> CreatePurchaseAsync(
        PurchaseDraftRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!TryDraft(request, out var draft, out var errors))
        {
            return Result<PurchaseResponse>.Fail(OperationFailure.Validation(errors));
        }

        var hash = Fingerprint(PurchaseOperations.PurchaseCreate, DraftCanonical(request.SupplierId, draft));
        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var claim = NewClaim(company.Id, PurchaseOperations.PurchaseCreate, idempotencyKey, hash);
        database.PurchaseIdempotencies.Add(claim);
        var claimed = await SaveClaimAsync(cancellationToken);
        if (claimed == ClaimSave.Conflict)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return await ReplayPurchaseAsync(company.Id, idempotencyKey, hash, cancellationToken);
        }

        if (claimed == ClaimSave.Failed)
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("The purchase idempotency claim could not be stored.");
        }

        var supplier = await database.Suppliers.SingleOrDefaultAsync(
            item => item.Id == request.SupplierId && item.CompanyId == company.Id,
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

        var lines = await ResolveLinesAsync(company.Id, draft.Lines, cancellationToken);
        if (lines.Error is not null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(lines.Error);
        }

        if (await DocumentTakenAsync(company.Id, supplier.Id, draft.SupplierDocumentKey, null, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseSupplierDocumentTaken));
        }

        var purchase = new Purchase
        {
            Id = Guid.CreateVersion7(),
            CompanyId = company.Id,
            SupplierId = supplier.Id,
            Number = await NextPurchaseNumberAsync(cancellationToken),
            Status = PurchaseStatuses.Draft,
            SupplierName = supplier.Name,
            SupplierTaxId = supplier.TaxId,
            SupplierDocumentRef = draft.SupplierDocumentRef,
            SupplierDocumentKey = draft.SupplierDocumentKey,
            Notes = draft.Notes,
            CreatedAtUtc = time.GetUtcNow(),
            CreatedByUserId = userId,
            Lines = BuildLines(Guid.Empty, lines.Lines),
        };
        foreach (var line in purchase.Lines)
        {
            line.PurchaseId = purchase.Id;
        }

        if (!PurchaseStatuses.CanMutate(purchase.Status))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseNotDraft));
        }

        database.Purchases.Add(purchase);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await database.Entry(purchase).ReloadAsync(cancellationToken);
            var response = ToPurchase(purchase, SellingPriceMap(lines.Lines));
            claim.PurchaseId = purchase.Id;
            claim.ResponseJson = JsonSerializer.Serialize(response, JsonOptions);
            await database.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result<PurchaseResponse>.Success(response);
        }
        catch (DbUpdateException exception) when (IsUnique(exception))
        {
            database.ChangeTracker.Clear();
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(UniqueFailure(exception));
        }
    }

    public async Task<Result<PurchaseResponse>> UpdatePurchaseAsync(
        Guid id,
        PurchaseDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Version is null)
        {
            return Result<PurchaseResponse>.Fail(OperationFailure.Validation([new FieldError("version", "version.required")]));
        }

        if (!TryDraft(request, out var draft, out var errors))
        {
            return Result<PurchaseResponse>.Fail(OperationFailure.Validation(errors));
        }

        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
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

        if (!PurchaseStatuses.CanMutate(purchase.Status))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseNotDraft));
        }

        if (purchase.Version != request.Version.Value)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseConflict));
        }

        var supplier = await database.Suppliers.SingleOrDefaultAsync(
            item => item.Id == request.SupplierId && item.CompanyId == company.Id,
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

        var lines = await ResolveLinesAsync(company.Id, draft.Lines, cancellationToken);
        if (lines.Error is not null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(lines.Error);
        }

        if (await DocumentTakenAsync(company.Id, supplier.Id, draft.SupplierDocumentKey, purchase.Id, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<PurchaseResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseSupplierDocumentTaken));
        }

        purchase.SupplierId = supplier.Id;
        purchase.SupplierName = supplier.Name;
        purchase.SupplierTaxId = supplier.TaxId;
        purchase.SupplierDocumentRef = draft.SupplierDocumentRef;
        purchase.SupplierDocumentKey = draft.SupplierDocumentKey;
        purchase.Notes = draft.Notes;
        var existingLines = purchase.Lines.ToList();
        database.PurchaseLines.RemoveRange(existingLines);
        // Replacing lines does not otherwise update this row, so xmin would not change.
        database.Entry(purchase).Property(item => item.Notes).IsModified = true;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            foreach (var line in existingLines)
            {
                purchase.Lines.Remove(line);
            }

            foreach (var line in BuildLines(purchase.Id, lines.Lines))
            {
                database.PurchaseLines.Add(line);
            }

            await database.SaveChangesAsync(cancellationToken);
            await database.Entry(purchase).ReloadAsync(cancellationToken);
            var response = ToPurchase(purchase, SellingPriceMap(lines.Lines));
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

    public async Task<Result<Guid>> DeletePurchaseAsync(Guid id, uint? version, CancellationToken cancellationToken)
    {
        if (version is null)
        {
            return Result<Guid>.Fail(OperationFailure.Validation([new FieldError("version", "version.required")]));
        }

        await using var tx = await database.Database.BeginTransactionAsync(cancellationToken);
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<Guid>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        await LockPurchaseAsync(company.Id, id, cancellationToken);
        var purchase = await database.Purchases
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);
        if (purchase is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<Guid>.Fail(OperationFailure.Of(404, ErrorCodes.PurchaseNotFound));
        }

        if (!PurchaseStatuses.CanMutate(purchase.Status))
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<Guid>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseNotDraft));
        }

        if (purchase.Version != version.Value)
        {
            await tx.RollbackAsync(cancellationToken);
            return Result<Guid>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseConflict));
        }

        database.PurchaseLines.RemoveRange(purchase.Lines);
        database.Purchases.Remove(purchase);
        await database.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result<Guid>.Success(id);
    }

    private async Task<Result<SupplierResponse>> ReplaySupplierAsync(
        Guid companyId,
        Guid idempotencyKey,
        string hash,
        CancellationToken cancellationToken)
    {
        var claim = await FindClaimAsync(companyId, PurchaseOperations.SupplierCreate, idempotencyKey, cancellationToken);
        if (claim is null || claim.RequestHash != hash || string.IsNullOrEmpty(claim.ResponseJson))
        {
            return Result<SupplierResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseIdempotencyConflict));
        }

        var stored = JsonSerializer.Deserialize<SupplierResponse>(claim.ResponseJson, JsonOptions);
        return stored is null
            ? Result<SupplierResponse>.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseIdempotencyConflict))
            : Result<SupplierResponse>.Success(stored);
    }

    private async Task<Result<PurchaseResponse>> ReplayPurchaseAsync(
        Guid companyId,
        Guid idempotencyKey,
        string hash,
        CancellationToken cancellationToken)
    {
        var claim = await FindClaimAsync(companyId, PurchaseOperations.PurchaseCreate, idempotencyKey, cancellationToken);
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

    private Task<PurchaseIdempotency?> FindClaimAsync(
        Guid companyId,
        string operation,
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        database.PurchaseIdempotencies.AsNoTracking().SingleOrDefaultAsync(
            item => item.CompanyId == companyId && item.Operation == operation && item.IdempotencyKey == idempotencyKey,
            cancellationToken);

    private async Task<ClaimSave> SaveClaimAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return ClaimSave.Stored;
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            return ClaimSave.Conflict;
        }
        catch (DbUpdateException)
        {
            return ClaimSave.Failed;
        }
    }

    private async Task<LineResolution> ResolveLinesAsync(
        Guid companyId,
        IReadOnlyList<DraftLine> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return LineResolution.Ready([]);
        }

        var ids = lines.Select(line => line.ProductId).ToArray();
        var products = await database.Products
            .Where(product => product.CompanyId == companyId && ids.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        var resolved = new List<ResolvedLine>(lines.Count);
        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                return LineResolution.Fail(OperationFailure.Of(404, ErrorCodes.ProductNotFound));
            }

            if (!product.IsActive)
            {
                return LineResolution.Fail(OperationFailure.Of(409, ErrorCodes.PurchaseProductInactive));
            }

            if (!product.TrackStock)
            {
                return LineResolution.Fail(OperationFailure.Of(409, ErrorCodes.StockNotTracked));
            }

            resolved.Add(new ResolvedLine(product, line.Quantity, line.UnitCost));
        }

        return LineResolution.Ready(resolved);
    }

    private static List<PurchaseLine> BuildLines(Guid purchaseId, IReadOnlyList<ResolvedLine> lines)
    {
        var built = new List<PurchaseLine>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            built.Add(new PurchaseLine
            {
                Id = Guid.CreateVersion7(),
                PurchaseId = purchaseId,
                LineNumber = index + 1,
                ProductId = line.Product.Id,
                QuantityOrdered = line.Quantity,
                QuantityReceived = 0m,
                UnitCost = line.UnitCost,
                ProductSku = line.Product.Sku,
                ProductName = line.Product.Name,
                ProductUnit = line.Product.Unit,
            });
        }

        return built;
    }

    private async Task<string> NextPurchaseNumberAsync(CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT nextval('pexilio.purchase_number_seq')";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return PurchaseNumbers.Format(Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }

    private Task LockPurchaseAsync(Guid companyId, Guid purchaseId, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM pexilio.purchase WHERE "CompanyId" = {companyId} AND "Id" = {purchaseId} FOR UPDATE""",
            cancellationToken);

    private Task<bool> SupplierNameTakenAsync(Guid companyId, string nameKey, Guid? exceptId, CancellationToken cancellationToken) =>
        database.Suppliers.AnyAsync(
            supplier => supplier.CompanyId == companyId && supplier.NameKey == nameKey && supplier.Id != exceptId,
            cancellationToken);

    private Task<bool> DocumentTakenAsync(
        Guid companyId,
        Guid supplierId,
        string? documentKey,
        Guid? exceptPurchaseId,
        CancellationToken cancellationToken)
    {
        if (documentKey is null)
        {
            return Task.FromResult(false);
        }

        return database.Purchases.AnyAsync(
            purchase => purchase.CompanyId == companyId
                && purchase.SupplierId == supplierId
                && purchase.SupplierDocumentKey == documentKey
                && purchase.Status != PurchaseStatuses.Cancelled
                && purchase.Id != exceptPurchaseId,
            cancellationToken);
    }

    private async Task<Dictionary<Guid, decimal>> SellingPricesAsync(
        Guid companyId,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await database.Products
            .Where(product => product.CompanyId == companyId && ids.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, product => product.UnitPrice, cancellationToken);
    }

    private static Dictionary<Guid, decimal> SellingPriceMap(IReadOnlyList<ResolvedLine> lines) =>
        lines.GroupBy(line => line.Product.Id).ToDictionary(group => group.Key, group => group.First().Product.UnitPrice);

    private PurchaseIdempotency NewClaim(Guid companyId, string operation, Guid idempotencyKey, string hash) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            Operation = operation,
            IdempotencyKey = idempotencyKey,
            RequestHash = hash,
            ResponseJson = "",
            CreatedAtUtc = time.GetUtcNow(),
        };

    private Task<Company?> CompanyAsync(CancellationToken cancellationToken) =>
        database.Companies.SingleOrDefaultAsync(cancellationToken);

    private static bool TryDraft(
        PurchaseDraftRequest request,
        out PurchaseDraftInput draft,
        out IReadOnlyList<FieldError> errors)
    {
        var lines = request.Lines?.Select(line => (line.ProductId, line.Quantity, line.UnitCost)).ToArray();
        return PurchasingValidator.TryDraft(request.SupplierDocumentRef, request.Notes, lines, out draft, out errors);
    }

    private static SupplierResponse ToSupplier(Supplier supplier) =>
        new(
            supplier.Id,
            supplier.Name,
            supplier.ContactName,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.TaxId,
            supplier.Notes,
            supplier.IsActive);

    private static PurchaseResponse ToPurchase(Purchase purchase, IReadOnlyDictionary<Guid, decimal> sellingPrices) =>
        new(
            purchase.Id,
            purchase.Number,
            purchase.Status,
            purchase.SupplierId,
            purchase.SupplierName,
            purchase.SupplierTaxId,
            purchase.SupplierDocumentRef,
            purchase.Notes,
            purchase.Version,
            purchase.CreatedAtUtc,
            purchase.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new PurchaseLineResponse(
                    line.Id,
                    line.LineNumber,
                    line.ProductId,
                    line.ProductSku,
                    line.ProductName,
                    line.ProductUnit,
                    QuantityText.Format(line.QuantityOrdered),
                    QuantityText.Format(line.QuantityReceived),
                    QuantityText.Format(line.UnitCost),
                    sellingPrices.TryGetValue(line.ProductId, out var price)
                        ? UnitPriceText.Format(price)
                        : QuantityText.Format(0m)))
                .ToList());

    private static string SupplierCanonical(SupplierDraft draft) =>
        string.Join(
            '\u001f',
            draft.NameKey,
            draft.ContactName ?? "",
            draft.Phone ?? "",
            draft.Email ?? "",
            draft.Address ?? "",
            draft.TaxId ?? "",
            draft.Notes ?? "",
            draft.IsActive ? "1" : "0");

    private static string DraftCanonical(Guid supplierId, PurchaseDraftInput draft)
    {
        var parts = new List<string>
        {
            supplierId.ToString("D"),
            draft.SupplierDocumentKey ?? "",
            draft.Notes ?? "",
        };
        foreach (var line in draft.Lines)
        {
            parts.Add(line.ProductId.ToString("D"));
            parts.Add(QuantityText.Format(line.Quantity));
            parts.Add(QuantityText.Format(line.UnitCost));
        }

        return string.Join('\u001f', parts);
    }

    private static string Fingerprint(string operation, string canonical)
    {
        var bytes = Encoding.UTF8.GetBytes(operation + '\u001f' + canonical);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool IsIdempotencyConflict(Exception exception) =>
        FindPostgres(exception) is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: not null } postgres
        && postgres.ConstraintName.Contains("idempotency", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnique(Exception exception) =>
        FindPostgres(exception) is { SqlState: PostgresErrorCodes.UniqueViolation };

    private static OperationFailure UniqueFailure(Exception exception)
    {
        var name = FindPostgres(exception)?.ConstraintName ?? "";
        if (name.Contains("supplier_document", StringComparison.OrdinalIgnoreCase))
        {
            return OperationFailure.Of(409, ErrorCodes.PurchaseSupplierDocumentTaken);
        }

        if (name.Contains("supplier", StringComparison.OrdinalIgnoreCase))
        {
            return OperationFailure.Of(409, ErrorCodes.SupplierNameTaken);
        }

        return OperationFailure.Of(409, ErrorCodes.PurchaseConflict);
    }

    private static PostgresException? FindPostgres(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    private enum ClaimSave
    {
        Stored,
        Conflict,
        Failed,
    }

    private sealed record ResolvedLine(Product Product, decimal Quantity, decimal UnitCost);

    private sealed record LineResolution(IReadOnlyList<ResolvedLine> Lines, OperationFailure? Error)
    {
        public static LineResolution Ready(IReadOnlyList<ResolvedLine> lines) => new(lines, null);

        public static LineResolution Fail(OperationFailure error) => new([], error);
    }
}
