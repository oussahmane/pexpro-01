namespace Pexilio.Domain.Sales;

public sealed class SaleLine
{
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid ProductId { get; set; }

    public int LineNumber { get; set; }

    public decimal Quantity { get; set; }

    public decimal ListUnitPrice { get; set; }

    public decimal UnitPrice { get; set; }

    public string PriceKind { get; set; } = PriceKinds.Retail;

    public decimal LineDiscount { get; set; }

    public string DiscountKind { get; set; } = DiscountKinds.None;

    public decimal? DiscountRate { get; set; }

    public bool BelowMinimumOverride { get; set; }

    public decimal AllocatedSaleDiscount { get; set; }

    public decimal LineTotal { get; set; }

    public string ProductSku { get; set; } = "";

    public string ProductName { get; set; } = "";

    public string ProductUnit { get; set; } = "";

    public bool TracksStock { get; set; }

    public Guid TaxRateId { get; set; }

    public decimal TaxRate { get; set; }

    public Guid? StockMovementId { get; set; }
}
