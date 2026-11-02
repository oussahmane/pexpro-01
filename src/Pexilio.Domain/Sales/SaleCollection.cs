namespace Pexilio.Domain.Sales;

public static class SaleCollection
{
    public const string CustomerRequired = "payment.customer_required";

    public const string Settled = "payment.settled";

    public const string Exceeds = "payment.exceeds";

    public static string? Problem(bool customerLinked, decimal outstanding, decimal amount)
    {
        if (!customerLinked)
        {
            return CustomerRequired;
        }

        if (outstanding <= 0m)
        {
            return Settled;
        }

        if (amount > outstanding)
        {
            return Exceeds;
        }

        return null;
    }
}
