namespace Pexilio.Domain.Sales;

public sealed class SaleIdempotency
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string Operation { get; set; } = "";

    public Guid IdempotencyKey { get; set; }

    public string RequestHash { get; set; } = "";

    public Guid? SaleId { get; set; }

    public string ResponseJson { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
