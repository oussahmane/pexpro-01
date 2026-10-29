namespace Pexilio.Domain.Company;

public sealed class CompanyInput
{
    public string? LegalName { get; set; }

    public string? TradeName { get; set; }

    public string? CurrencyCode { get; set; }

    public int? CurrencyScale { get; set; }

    public string? TimeZoneId { get; set; }

    public string? TaxMode { get; set; }

    public string? DefaultTaxRate { get; set; }

    public string? Rc { get; set; }

    public string? Nif { get; set; }

    public string? Nis { get; set; }

    public string? Ai { get; set; }
}

public sealed record CompanyProfile(
    string LegalName,
    string? TradeName,
    string CurrencyCode,
    int CurrencyScale,
    string TimeZoneId,
    TaxMode TaxMode,
    decimal DefaultTaxRate,
    string? Rc,
    string? Nif,
    string? Nis,
    string? Ai);
