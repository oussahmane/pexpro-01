namespace Pexilio.Domain.Purchasing;

public static class PurchaseTransitions
{
    public static bool CanOrder(string status, int lineCount) =>
        status == PurchaseStatuses.Draft && lineCount > 0;

    public static bool CanReceive(string status) =>
        status is PurchaseStatuses.Ordered or PurchaseStatuses.PartiallyReceived;

    public static bool CanCancel(string status, bool anyQuantityReceived, bool anyReceipt) =>
        status == PurchaseStatuses.Ordered && !anyQuantityReceived && !anyReceipt;

    public static string AfterReceipt(bool fullyReceived) =>
        fullyReceived ? PurchaseStatuses.Received : PurchaseStatuses.PartiallyReceived;

    public static bool WithinRemaining(decimal quantity, decimal remaining) =>
        quantity > 0m && quantity <= remaining;

    public static bool OneMovementPerProduct(IEnumerable<Guid> productIds)
    {
        var seen = new HashSet<Guid>();
        foreach (var productId in productIds)
        {
            if (!seen.Add(productId))
            {
                return false;
            }
        }

        return true;
    }
}
