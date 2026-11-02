namespace Pexilio.Domain.Stock;

public static class StockMovementTypes
{
    public const string Opening = "opening";

    public const string Receipt = "receipt";

    public const string Adjustment = "adjustment";

    public const string Sale = "sale";

    public const string SaleReturn = "sale_return";

    public const string Manual = "manual";
}

public static class StockSourceTypes
{
    public const string Manual = StockMovementTypes.Manual;

    public const string Purchase = "purchase";

    public const string Sale = "sale";

    public const string SaleReturn = "sale_return";

    public static bool IsKnown(string? sourceType) =>
        sourceType is Manual or Purchase or Sale or SaleReturn;
}
