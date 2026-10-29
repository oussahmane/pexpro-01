using Pexilio.Domain.Company;

namespace Pexilio.Application;

public interface ISetupService
{
    Task<bool> IsCompleteAsync(CancellationToken cancellationToken);

    Task<Result<SetupCreated>> SetupAsync(SetupRequest request, CancellationToken cancellationToken);
}

public interface ISessionService
{
    Task<Result<TokenPair>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<TokenPair>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken);
}

public interface ICompanyService
{
    Task<CompanyResponse?> GetAsync(CancellationToken cancellationToken);

    Task<Result<CompanyResponse>> UpdateAsync(CompanyInput request, CancellationToken cancellationToken);
}

public interface ICashierService
{
    Task<Result<Guid>> CreateAsync(LoginRequest request, CancellationToken cancellationToken);
}

public interface ICatalogService
{
    Task<Result<IReadOnlyList<CategoryResponse>>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<Result<CategoryResponse>> CreateCategoryAsync(CategoryRequest request, CancellationToken cancellationToken);

    Task<Result<CategoryResponse>> UpdateCategoryAsync(Guid id, CategoryRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ProductResponse>>> ListProductsAsync(CancellationToken cancellationToken);

    Task<Result<ProductResponse>> CreateProductAsync(ProductRequest request, CancellationToken cancellationToken);

    Task<Result<ProductResponse>> UpdateProductAsync(Guid id, ProductRequest request, CancellationToken cancellationToken);
}

public interface IStockService
{
    Task<Result<IReadOnlyList<StockLocationResponse>>> ListLocationsAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<StockBalanceResponse>>> ListBalancesAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<StockMovementResponse>>> ListMovementsAsync(Guid? productId, CancellationToken cancellationToken);

    Task<Result<StockMovementResponse>> OpenAsync(
        StockQuantityRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<StockMovementResponse>> ReceiveAsync(
        StockQuantityRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<StockMovementResponse>> AdjustAsync(
        StockQuantityRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<StockBalanceResponse>> SetMinimumAsync(
        Guid productId,
        MinimumStockRequest request,
        CancellationToken cancellationToken);
}

public interface IPurchasingService
{
    Task<Result<IReadOnlyList<SupplierResponse>>> ListSuppliersAsync(CancellationToken cancellationToken);

    Task<Result<SupplierResponse>> GetSupplierAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<SupplierResponse>> CreateSupplierAsync(
        SupplierRequest request,
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<Result<SupplierResponse>> UpdateSupplierAsync(
        Guid id,
        SupplierRequest request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PurchaseSummaryResponse>>> ListPurchasesAsync(
        string? status,
        CancellationToken cancellationToken);

    Task<Result<PurchaseResponse>> GetPurchaseAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<PurchaseResponse>> CreatePurchaseAsync(
        PurchaseDraftRequest request,
        Guid idempotencyKey,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<PurchaseResponse>> UpdatePurchaseAsync(
        Guid id,
        PurchaseDraftRequest request,
        CancellationToken cancellationToken);

    Task<Result<Guid>> DeletePurchaseAsync(Guid id, uint? version, CancellationToken cancellationToken);
}
