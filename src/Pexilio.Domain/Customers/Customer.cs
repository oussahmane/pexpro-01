namespace Pexilio.Domain.Customers;

public sealed class Customer
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string Type { get; set; } = CustomerTypes.Individual;

    public string Name { get; set; } = "";

    public string NameKey { get; set; } = "";

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public string? Rc { get; set; }

    public string? Nif { get; set; }

    public string? Nis { get; set; }

    public string? Ai { get; set; }

    public decimal? CreditLimit { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
