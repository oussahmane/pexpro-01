using System.Globalization;

namespace Pexilio.Domain.Sales;

public static class PaymentReceiptNumbers
{
    public const string Series = "customer_payment";

    public static string Format(long value)
    {
        var digits = value.ToString(CultureInfo.InvariantCulture);
        if (digits.Length < 6)
        {
            digits = digits.PadLeft(6, '0');
        }

        return "R-" + digits;
    }
}
