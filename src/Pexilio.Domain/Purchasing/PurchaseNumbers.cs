using System.Globalization;

namespace Pexilio.Domain.Purchasing;

public static class PurchaseNumbers
{
    public static string Format(long value)
    {
        var digits = value.ToString(CultureInfo.InvariantCulture);
        if (digits.Length < 6)
        {
            digits = digits.PadLeft(6, '0');
        }

        return "A-" + digits;
    }
}
