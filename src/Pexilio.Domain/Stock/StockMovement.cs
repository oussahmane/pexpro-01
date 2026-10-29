namespace Pexilio.Domain.Stock;

public sealed class StockMovement
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid StockLocationId { get; set; }

    public Guid ProductId { get; set; }

    public string MovementType { get; set; } = "";

    public decimal QuantityDelta { get; set; }

    public string Reason { get; set; } = "";

    public decimal BalanceAfter { get; set; }

    public decimal? UnitCost { get; set; }

    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }
}
