namespace Pexilio.Domain.Purchasing;

public sealed class PurchaseLine
{
    public Guid Id { get; set; }

    public Guid PurchaseId { get; set; }

    public int LineNumber { get; set; }

    public Guid ProductId { get; set; }

    public decimal QuantityOrdered { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal UnitCost { get; set; }

    public string ProductSku { get; set; } = "";

    public string ProductName { get; set; } = "";

    public string ProductUnit { get; set; } = "";
}
