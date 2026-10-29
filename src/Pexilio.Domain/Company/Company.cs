namespace Pexilio.Domain.Company;

/// <summary>
/// One installation has one company. The database allows only SingletonKey 1.
/// </summary>
public sealed class Company
{
    public Guid Id { get; set; }

    public int SingletonKey { get; set; } = 1;

    public string LegalName { get; set; } = "";

    public string? TradeName { get; set; }

    public string CurrencyCode { get; set; } = "DZD";

    public int CurrencyScale { get; set; } = 2;

    public string TimeZoneId { get; set; } = "Africa/Algiers";

    public TaxMode TaxMode { get; set; } = TaxMode.Exclusive;

    public string? Rc { get; set; }

    public string? Nif { get; set; }

    public string? Nis { get; set; }

    public string? Ai { get; set; }

    public List<TaxRate> TaxRates { get; set; } = [];
}
