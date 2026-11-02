namespace Pexilio.Domain.Sales;

public sealed class DocumentCounter
{
    public Guid CompanyId { get; set; }

    public string Series { get; set; } = "";

    public long LastValue { get; set; }
}
