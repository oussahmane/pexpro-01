using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pexilio.Application;
using Pexilio.Domain;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Company;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Infrastructure.Catalog;

public sealed class CatalogService(PexilioDbContext database) : ICatalogService
{
    public async Task<Result<IReadOnlyList<CategoryResponse>>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<IReadOnlyList<CategoryResponse>>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var rows = await database.Categories
            .Where(category => category.CompanyId == company.Id)
            .OrderBy(category => category.Name)
            .Select(category => new CategoryResponse(category.Id, category.Name, category.ParentId))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CategoryResponse>>.Success(rows);
    }

    public async Task<Result<CategoryResponse>> CreateCategoryAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        if (!CatalogValidator.TryCategoryName(request.Name, out var name, out var key, out var errors))
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Validation(errors));
        }

        if (!await ParentExistsAsync(company.Id, request.ParentId, cancellationToken))
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CategoryNotFound));
        }

        if (await NameTakenAsync(company.Id, request.ParentId, key, null, cancellationToken))
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Of(409, ErrorCodes.CategoryNameTaken));
        }

        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            CompanyId = company.Id,
            ParentId = request.ParentId,
            Name = name,
            NameKey = key,
        };
        database.Categories.Add(category);
        var saved = await SaveAsync(cancellationToken);
        if (saved is not null)
        {
            return Result<CategoryResponse>.Fail(saved);
        }

        return Result<CategoryResponse>.Success(new CategoryResponse(category.Id, category.Name, category.ParentId));
    }

    public async Task<Result<CategoryResponse>> UpdateCategoryAsync(Guid id, CategoryRequest request, CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var category = await database.Categories.SingleOrDefaultAsync(
            item => item.Id == id && item.CompanyId == company.Id,
            cancellationToken);
        if (category is null)
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CategoryNotFound));
        }

        if (!CatalogValidator.TryCategoryName(request.Name, out var name, out var key, out var errors))
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Validation(errors));
        }

        if (!await ParentExistsAsync(company.Id, request.ParentId, cancellationToken))
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CategoryNotFound));
        }

        var parents = await database.Categories
            .Where(item => item.CompanyId == company.Id)
            .Select(item => new { item.Id, item.ParentId })
            .ToDictionaryAsync(item => item.Id, item => item.ParentId, cancellationToken);
        if (CategoryGraph.CreatesCycle(category.Id, request.ParentId, current => parents.GetValueOrDefault(current)))
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Of(409, ErrorCodes.CategoryCycle));
        }

        if (await NameTakenAsync(company.Id, request.ParentId, key, category.Id, cancellationToken))
        {
            return Result<CategoryResponse>.Fail(OperationFailure.Of(409, ErrorCodes.CategoryNameTaken));
        }

        category.Name = name;
        category.NameKey = key;
        category.ParentId = request.ParentId;
        var saved = await SaveAsync(cancellationToken);
        if (saved is not null)
        {
            return Result<CategoryResponse>.Fail(saved);
        }

        return Result<CategoryResponse>.Success(new CategoryResponse(category.Id, category.Name, category.ParentId));
    }

    public async Task<Result<IReadOnlyList<ProductResponse>>> ListProductsAsync(CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<IReadOnlyList<ProductResponse>>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var rates = await RateMapAsync(company.Id, cancellationToken);
        var rows = await database.Products
            .Where(product => product.CompanyId == company.Id)
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Sku)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<ProductResponse>>.Success(rows.Select(product => ToResponse(product, rates)).ToList());
    }

    public Task<Result<ProductResponse>> CreateProductAsync(ProductRequest request, CancellationToken cancellationToken) =>
        SaveProductAsync(null, request, cancellationToken);

    public Task<Result<ProductResponse>> UpdateProductAsync(Guid id, ProductRequest request, CancellationToken cancellationToken) =>
        SaveProductAsync(id, request, cancellationToken);

    private async Task<Result<ProductResponse>> SaveProductAsync(Guid? id, ProductRequest request, CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result<ProductResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        if (!CatalogValidator.TryProduct(request.Sku, request.Name, request.Unit, request.UnitPrice, request.TrackStock, request.IsActive, out var draft, out var errors))
        {
            return Result<ProductResponse>.Fail(OperationFailure.Validation(errors));
        }

        Product? existing = null;
        if (id is Guid existingId)
        {
            existing = await database.Products.SingleOrDefaultAsync(
                item => item.Id == existingId && item.CompanyId == company.Id,
                cancellationToken);
            if (existing is null)
            {
                return Result<ProductResponse>.Fail(OperationFailure.Of(404, ErrorCodes.ProductNotFound));
            }
        }

        if (request.CategoryId is Guid categoryId)
        {
            var categoryExists = await database.Categories.AnyAsync(
                category => category.Id == categoryId && category.CompanyId == company.Id,
                cancellationToken);
            if (!categoryExists)
            {
                return Result<ProductResponse>.Fail(OperationFailure.Validation([new FieldError("categoryId", "categoryId.unknown")]));
            }
        }

        var taxRateId = request.TaxRateId ?? await database.TaxRates
            .Where(rate => rate.CompanyId == company.Id && rate.IsDefault)
            .Select(rate => (Guid?)rate.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (taxRateId is null || !await database.TaxRates.AnyAsync(rate => rate.Id == taxRateId && rate.CompanyId == company.Id, cancellationToken))
        {
            return Result<ProductResponse>.Fail(OperationFailure.Validation([new FieldError("taxRateId", "taxRateId.unknown")]));
        }

        var productId = existing?.Id ?? Guid.Empty;
        var skuTaken = await database.Products.AnyAsync(
            item => item.CompanyId == company.Id && item.SkuKey == draft.SkuKey && item.Id != productId,
            cancellationToken);
        if (skuTaken)
        {
            return Result<ProductResponse>.Fail(OperationFailure.Of(409, ErrorCodes.SkuTaken));
        }

        var product = existing ?? new Product { Id = Guid.CreateVersion7(), CompanyId = company.Id };
        if (existing is null)
        {
            database.Products.Add(product);
        }

        product.CategoryId = request.CategoryId;
        product.TaxRateId = taxRateId.Value;
        product.Sku = draft.Sku;
        product.SkuKey = draft.SkuKey;
        product.Name = draft.Name;
        product.Unit = draft.Unit;
        product.UnitPrice = draft.UnitPrice;
        product.TrackStock = draft.TrackStock;
        product.IsActive = draft.IsActive;

        var saved = await SaveAsync(cancellationToken);
        if (saved is not null)
        {
            return Result<ProductResponse>.Fail(saved);
        }

        var rates = await RateMapAsync(company.Id, cancellationToken);
        return Result<ProductResponse>.Success(ToResponse(product, rates));
    }

    private async Task<Company?> CompanyAsync(CancellationToken cancellationToken) =>
        await database.Companies.SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> ParentExistsAsync(Guid companyId, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return true;
        }

        return await database.Categories.AnyAsync(
            category => category.Id == parentId && category.CompanyId == companyId,
            cancellationToken);
    }

    private Task<bool> NameTakenAsync(Guid companyId, Guid? parentId, string key, Guid? exceptId, CancellationToken cancellationToken) =>
        database.Categories.AnyAsync(
            category => category.CompanyId == companyId
                && category.ParentId == parentId
                && category.NameKey == key
                && category.Id != exceptId,
            cancellationToken);

    private async Task<Dictionary<Guid, decimal>> RateMapAsync(Guid companyId, CancellationToken cancellationToken) =>
        await database.TaxRates
            .Where(rate => rate.CompanyId == companyId)
            .ToDictionaryAsync(rate => rate.Id, rate => rate.Rate, cancellationToken);

    private async Task<OperationFailure?> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            database.ChangeTracker.Clear();
            var code = postgres.ConstraintName?.Contains("Sku", StringComparison.Ordinal) == true
                ? ErrorCodes.SkuTaken
                : ErrorCodes.CategoryNameTaken;
            return OperationFailure.Of(409, code);
        }
    }

    private static ProductResponse ToResponse(Product product, IReadOnlyDictionary<Guid, decimal> rates)
    {
        var rate = rates.GetValueOrDefault(product.TaxRateId);
        return new ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Unit,
            UnitPriceText.Format(product.UnitPrice),
            product.CategoryId,
            product.TaxRateId,
            rate.ToString("0.######", CultureInfo.InvariantCulture),
            product.TrackStock,
            product.IsActive);
    }
}
