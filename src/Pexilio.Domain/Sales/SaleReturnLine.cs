namespace Pexilio.Domain.Sales;

public sealed class SaleReturnLine
{
    public Guid Id { get; set; }

    public Guid ReturnId { get; set; }

    public Guid SaleId { get; set; }

    public Guid SaleLineId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal Amount { get; set; }

    public bool TracksStock { get; set; }

    public Guid? StockMovementId { get; set; }
}
