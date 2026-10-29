namespace Pexilio.Domain.Purchasing;

public static class PurchaseStatuses
{
    public const string Draft = "draft";

    public const string Ordered = "ordered";

    public const string PartiallyReceived = "partially_received";

    public const string Received = "received";

    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Draft,
        Ordered,
        PartiallyReceived,
        Received,
        Cancelled,
    };

    public static bool CanMutate(string status) => status == Draft;
}

public enum PurchaseReplayOutcome
{
    ReturnStored,
    Missing,
    Conflict,
}

public static class PurchaseReplay
{
    public static PurchaseReplayOutcome Decide(bool sameRequest, bool hasStoredResponse, bool purchaseExists)
    {
        if (!sameRequest || !hasStoredResponse)
        {
            return PurchaseReplayOutcome.Conflict;
        }

        return purchaseExists ? PurchaseReplayOutcome.ReturnStored : PurchaseReplayOutcome.Missing;
    }
}
