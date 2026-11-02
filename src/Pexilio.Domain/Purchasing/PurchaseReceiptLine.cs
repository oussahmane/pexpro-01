namespace Pexilio.Domain.Purchasing;

public sealed class PurchaseReceiptLine
{
    public Guid Id { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid PurchaseId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid PurchaseLineId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public Guid StockMovementId { get; set; }
}
