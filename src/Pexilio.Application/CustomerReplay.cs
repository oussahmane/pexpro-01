using Pexilio.Domain.Stock;

namespace Pexilio.Application;

public static class CustomerReplay
{
    public static bool TryWithCurrentOutstanding(
        CustomerResponse stored,
        decimal currentOutstanding,
        out CustomerResponse response)
    {
        if (currentOutstanding < 0m)
        {
            response = stored;
            return false;
        }

        response = stored with { OutstandingAmount = QuantityText.Format(currentOutstanding) };
        return true;
    }
}
