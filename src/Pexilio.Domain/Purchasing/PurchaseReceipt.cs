namespace Pexilio.Domain.Purchasing;

public sealed class PurchaseReceipt
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid PurchaseId { get; set; }

    public string Number { get; set; } = "";

    public string DocumentRef { get; set; } = "";

    public string DocumentKey { get; set; } = "";

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public Guid ReceivedByUserId { get; set; }

    public List<PurchaseReceiptLine> Lines { get; set; } = [];
}
