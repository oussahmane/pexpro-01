using Pexilio.Domain.Company;

namespace Pexilio.Application;

public sealed class SetupRequest
{
    public string? LegalName { get; set; }

    public string? TradeName { get; set; }

    public string? CurrencyCode { get; set; }

    public int? CurrencyScale { get; set; }

    public string? TimeZoneId { get; set; }

    public string? TaxMode { get; set; }

    public string? DefaultTaxRate { get; set; }

    public string? Rc { get; set; }

    public string? Nif { get; set; }

    public string? Nis { get; set; }

    public string? Ai { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public CompanyInput ToCompanyInput() => new()
    {
        LegalName = LegalName,
        TradeName = TradeName,
        CurrencyCode = CurrencyCode,
        CurrencyScale = CurrencyScale,
        TimeZoneId = TimeZoneId,
        TaxMode = TaxMode,
        DefaultTaxRate = DefaultTaxRate,
        Rc = Rc,
        Nif = Nif,
        Nis = Nis,
        Ai = Ai,
    };
}

public sealed class LoginRequest
{
    public string? Email { get; set; }

    public string? Password { get; set; }
}

public sealed class RefreshRequest
{
    public string? RefreshToken { get; set; }
}

public sealed record SetupCreated(Guid CompanyId, Guid AdministratorId);

public sealed record TokenPair(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record CategoryRequest(string? Name, Guid? ParentId);

public sealed record CategoryResponse(Guid Id, string Name, Guid? ParentId);

public sealed record ProductRequest(
    string? Sku,
    string? Name,
    string? Unit,
    string? UnitPrice,
    Guid? CategoryId,
    Guid? TaxRateId,
    bool? TrackStock,
    bool? IsActive);

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string Unit,
    string UnitPrice,
    Guid? CategoryId,
    Guid TaxRateId,
    string TaxRate,
    bool TrackStock,
    bool IsActive);

public sealed class StockQuantityRequest
{
    public Guid ProductId { get; set; }

    public string? Quantity { get; set; }

    public string? Reason { get; set; }

    public string? UnitCost { get; set; }
}

public sealed class MinimumStockRequest
{
    public string? MinimumQuantity { get; set; }
}

public sealed record StockLocationResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsDefault,
    bool PreventNegativeStock);

public sealed record StockBalanceResponse(
    Guid ProductId,
    string Sku,
    string Name,
    string Unit,
    string Quantity,
    string? MinimumQuantity,
    bool BelowMinimum,
    bool AtMinimum);

public sealed record StockMovementResponse(
    Guid Id,
    Guid ProductId,
    Guid StockLocationId,
    string MovementType,
    string QuantityDelta,
    string Reason,
    string BalanceAfter,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);

public sealed class SupplierRequest
{
    public string? Name { get; set; }

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? TaxId { get; set; }

    public string? Notes { get; set; }

    public bool? IsActive { get; set; }
}

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? TaxId,
    string? Notes,
    bool IsActive);

public sealed class PurchaseLineRequest
{
    public Guid ProductId { get; set; }

    public string? Quantity { get; set; }

    public string? UnitCost { get; set; }
}

public sealed class PurchaseDraftRequest
{
    public Guid SupplierId { get; set; }

    public string? SupplierDocumentRef { get; set; }

    public string? Notes { get; set; }

    public uint? Version { get; set; }

    public List<PurchaseLineRequest>? Lines { get; set; }
}

public sealed record PurchaseLineResponse(
    Guid Id,
    int LineNumber,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string ProductUnit,
    string QuantityOrdered,
    string QuantityReceived,
    string UnitCost,
    string SellingPrice);

public sealed record PurchaseSummaryResponse(
    Guid Id,
    string Number,
    string Status,
    Guid SupplierId,
    string SupplierName,
    string? SupplierDocumentRef,
    DateTimeOffset CreatedAt);

public sealed record PurchaseResponse(
    Guid Id,
    string Number,
    string Status,
    Guid SupplierId,
    string SupplierName,
    string? SupplierTaxId,
    string? SupplierDocumentRef,
    string? Notes,
    uint Version,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseLineResponse> Lines);

public sealed record CompanyResponse(
    Guid Id,
    string LegalName,
    string? TradeName,
    string CurrencyCode,
    int CurrencyScale,
    string TimeZoneId,
    string TaxMode,
    string DefaultTaxRate,
    string? Rc,
    string? Nif,
    string? Nis,
    string? Ai);
