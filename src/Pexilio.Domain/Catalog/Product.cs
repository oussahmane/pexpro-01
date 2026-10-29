namespace Pexilio.Domain.Catalog;

public sealed class Product
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid TaxRateId { get; set; }

    public string Sku { get; set; } = "";

    public string SkuKey { get; set; } = "";

    public string Name { get; set; } = "";

    public string Unit { get; set; } = "";

    public decimal UnitPrice { get; set; }

    public bool TrackStock { get; set; } = true;

    public bool IsActive { get; set; } = true;
}
