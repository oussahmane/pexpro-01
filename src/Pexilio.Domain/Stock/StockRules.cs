namespace Pexilio.Domain.Stock;

public static class StockRules
{
    public const int ReasonMaxLength = 200;

    public const string DefaultLocationCode = "MAIN";

    public const string DefaultLocationName = "Principal";

    public static bool TryReason(string? text, out string reason, out string code)
    {
        reason = text?.Trim() ?? "";
        if (reason.Length == 0)
        {
            code = "reason.required";
            return false;
        }

        if (reason.Length > ReasonMaxLength)
        {
            code = "reason.length";
            return false;
        }

        code = "";
        return true;
    }

    public static bool TryDelta(string movementType, decimal quantity, out string code)
    {
        if (quantity == 0m)
        {
            code = "quantity.zero";
            return false;
        }

        var requiresPositive = movementType is StockMovementTypes.Opening
            or StockMovementTypes.Receipt
            or StockMovementTypes.SaleReturn;
        if (requiresPositive && quantity < 0m)
        {
            code = "quantity.positive";
            return false;
        }

        if (movementType == StockMovementTypes.Sale && quantity > 0m)
        {
            code = "quantity.sign";
            return false;
        }

        var known = movementType is StockMovementTypes.Opening
            or StockMovementTypes.Receipt
            or StockMovementTypes.Adjustment
            or StockMovementTypes.Sale
            or StockMovementTypes.SaleReturn;
        if (!known)
        {
            code = "quantity.invalid";
            return false;
        }

        code = "";
        return true;
    }

    public static bool BelowMinimum(decimal quantity, decimal? minimum) =>
        minimum is not null && quantity < minimum.Value;

    public static bool AtMinimum(decimal quantity, decimal? minimum) =>
        minimum is not null && quantity == minimum.Value;
}
