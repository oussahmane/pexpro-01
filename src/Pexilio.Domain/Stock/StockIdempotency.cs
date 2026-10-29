namespace Pexilio.Domain.Stock;

public sealed class StockIdempotency
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid IdempotencyKey { get; set; }

    public string RequestHash { get; set; } = "";

    public Guid? MovementId { get; set; }

    public string ResponseJson { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
