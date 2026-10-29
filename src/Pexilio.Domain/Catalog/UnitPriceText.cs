using System.Globalization;

namespace Pexilio.Domain.Catalog;

public static class UnitPriceText
{
    public const int Scale = 4;

    private const decimal Maximum = 999999999999999.9999m;

    public static bool TryParse(string? text, out decimal value, out string code)
    {
        value = 0m;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            code = "unitPrice.required";
            return false;
        }

        if (!decimal.TryParse(trimmed, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            code = "unitPrice.invalid";
            return false;
        }

        if (value < 0m || value > Maximum)
        {
            code = "unitPrice.range";
            return false;
        }

        var scale = (decimal.GetBits(value)[3] >> 16) & 0xFF;
        if (scale > Scale)
        {
            code = "unitPrice.scale";
            return false;
        }

        code = "";
        return true;
    }

    public static string Format(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
