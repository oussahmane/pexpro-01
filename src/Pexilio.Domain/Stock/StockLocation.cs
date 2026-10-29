namespace Pexilio.Domain.Stock;

public sealed class StockLocation
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public bool IsDefault { get; set; }

    public bool PreventNegativeStock { get; set; } = true;
}
