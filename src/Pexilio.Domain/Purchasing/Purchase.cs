namespace Pexilio.Domain.Purchasing;

public sealed class Purchase
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid SupplierId { get; set; }

    public string Number { get; set; } = "";

    public string Status { get; set; } = PurchaseStatuses.Draft;

    public string SupplierName { get; set; } = "";

    public string? SupplierTaxId { get; set; }

    public string? SupplierDocumentRef { get; set; }

    public string? SupplierDocumentKey { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? OrderedAtUtc { get; set; }

    public DateTimeOffset? ReceivedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }

    public uint Version { get; set; }

    public List<PurchaseLine> Lines { get; set; } = [];
}
