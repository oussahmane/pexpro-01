namespace Pexilio.Domain.Customers;

public sealed class CustomerIdempotency
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string Operation { get; set; } = "";

    public Guid IdempotencyKey { get; set; }

    public string RequestHash { get; set; } = "";

    public string ResponseJson { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
