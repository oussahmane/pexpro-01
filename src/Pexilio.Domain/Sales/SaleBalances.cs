namespace Pexilio.Domain.Sales;

public readonly record struct SaleBalance(decimal Net, decimal Paid, decimal Outstanding, string State);

public static class SaleBalances
{
    public static bool TryDerive(decimal total, decimal returned, decimal paid, out SaleBalance balance)
    {
        balance = default;
        if (total < 0m || returned < 0m || paid < 0m || returned > total)
        {
            return false;
        }

        var net = total - returned;
        var outstanding = net - paid;
        if (outstanding < 0m)
        {
            return false;
        }

        var state = outstanding == 0m
            ? SalePaymentStates.Paid
            : paid == 0m
                ? SalePaymentStates.Unpaid
                : SalePaymentStates.Partial;
        balance = new SaleBalance(net, paid, outstanding, state);
        return true;
    }
}
