using System.Globalization;

namespace Pexilio.Domain.Stock;

public static class QuantityText
{
    public const int Scale = 4;

    public const decimal Maximum = 999999999999999.9999m;

    public static bool TryParse(string? text, string field, bool allowNegative, out decimal value, out string code)
    {
        value = 0m;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            code = $"{field}.required";
            return false;
        }

        if (trimmed.Contains('e') || trimmed.Contains('E') || trimmed.Contains(',') || trimmed.Contains(' '))
        {
            code = $"{field}.invalid";
            return false;
        }

        if (!decimal.TryParse(
                trimmed,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value))
        {
            code = $"{field}.invalid";
            return false;
        }

        if (!allowNegative && value < 0m)
        {
            code = $"{field}.sign";
            return false;
        }

        if (value > Maximum || value < -Maximum)
        {
            code = $"{field}.range";
            return false;
        }

        var scale = (decimal.GetBits(value)[3] >> 16) & 0xFF;
        if (scale > Scale)
        {
            code = $"{field}.scale";
            return false;
        }

        code = "";
        return true;
    }

    public static string Format(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
