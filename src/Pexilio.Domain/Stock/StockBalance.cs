namespace Pexilio.Domain.Stock;

public sealed class StockBalance
{
    public Guid Id { get; set; }

    public Guid StockLocationId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? MinimumQuantity { get; set; }
}
