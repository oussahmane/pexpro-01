namespace Pexilio.Domain.Company;

/// <summary>
/// A named rate for one company. Exactly one row has <see cref="IsDefault"/>.
/// Later product or document lines can reference <see cref="Id"/> and snapshot
/// <see cref="Rate"/> without changing this table.
/// </summary>
public sealed class TaxRate
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public string Name { get; set; } = "default";

    public decimal Rate { get; set; }

    public bool IsDefault { get; set; }
}
