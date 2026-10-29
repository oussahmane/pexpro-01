using System.Globalization;
using System.Net.Mail;
using Pexilio.Domain;

namespace Pexilio.Domain.Company;

public static class CompanyProfileValidator
{
    public const string DefaultCurrencyCode = "DZD";
    public const int DefaultCurrencyScale = 2;
    public const string DefaultTimeZoneId = "Africa/Algiers";
    public const int NameMaxLength = 200;
    public const int IdentifierMaxLength = 64;
    public const int TimeZoneMaxLength = 64;

    public static bool TryNormalize(
        CompanyInput input,
        out CompanyProfile? profile,
        out IReadOnlyList<FieldError> errors)
    {
        var found = new List<FieldError>();
        var legalName = NormalizeRequired(input.LegalName, "legalName", NameMaxLength, found);
        var tradeName = NormalizeOptional(input.TradeName, "tradeName", NameMaxLength, found);
        var currencyCode = NormalizeCurrency(input.CurrencyCode, found);
        var currencyScale = NormalizeScale(input.CurrencyScale, found);
        var timeZoneId = NormalizeTimeZone(input.TimeZoneId, found);
        var taxMode = NormalizeTaxMode(input.TaxMode, found);
        var taxRate = NormalizeRate(input.DefaultTaxRate, found);
        var rc = NormalizeOptional(input.Rc, "rc", IdentifierMaxLength, found);
        var nif = NormalizeOptional(input.Nif, "nif", IdentifierMaxLength, found);
        var nis = NormalizeOptional(input.Nis, "nis", IdentifierMaxLength, found);
        var ai = NormalizeOptional(input.Ai, "ai", IdentifierMaxLength, found);

        if (found.Count > 0 || legalName is null || currencyCode is null || timeZoneId is null || taxRate is null)
        {
            profile = null;
            errors = found;
            return false;
        }

        profile = new CompanyProfile(
            legalName,
            tradeName,
            currencyCode,
            currencyScale,
            timeZoneId,
            taxMode,
            taxRate.Value,
            rc,
            nif,
            nis,
            ai);
        errors = found;
        return true;
    }

    private static string? NormalizeRequired(string? value, string field, int maxLength, List<FieldError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new FieldError(field, $"{field}.required"));
            return null;
        }

        return NormalizeText(trimmed, field, maxLength, errors);
    }

    private static string? NormalizeOptional(string? value, string field, int maxLength, List<FieldError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return NormalizeText(trimmed, field, maxLength, errors);
    }

    private static string? NormalizeText(string value, string field, int maxLength, List<FieldError> errors)
    {
        if (value.Any(char.IsControl))
        {
            errors.Add(new FieldError(field, $"{field}.invalid"));
            return null;
        }

        if (value.Length > maxLength)
        {
            errors.Add(new FieldError(field, $"{field}.too_long"));
            return null;
        }

        return value;
    }

    private static string? NormalizeCurrency(string? value, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultCurrencyCode;
        }

        var code = value.Trim().ToUpperInvariant();
        if (code.Length != 3 || code.Any(character => character is < 'A' or > 'Z'))
        {
            errors.Add(new FieldError("currencyCode", "currencyCode.invalid"));
            return null;
        }

        return code;
    }

    private static int NormalizeScale(int? value, List<FieldError> errors)
    {
        var scale = value ?? DefaultCurrencyScale;
        if (scale is < 0 or > 4)
        {
            errors.Add(new FieldError("currencyScale", "currencyScale.invalid"));
            return DefaultCurrencyScale;
        }

        return scale;
    }

    private static string? NormalizeTimeZone(string? value, List<FieldError> errors)
    {
        var timeZoneId = string.IsNullOrWhiteSpace(value) ? DefaultTimeZoneId : value.Trim();
        if (timeZoneId.Length > TimeZoneMaxLength || timeZoneId.Any(char.IsControl))
        {
            errors.Add(new FieldError("timeZoneId", "timeZoneId.invalid"));
            return null;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            errors.Add(new FieldError("timeZoneId", "timeZoneId.invalid"));
            return null;
        }

        return timeZoneId;
    }

    private static TaxMode NormalizeTaxMode(string? value, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TaxMode.Exclusive;
        }

        if (value.Trim().Equals("exclusive", StringComparison.OrdinalIgnoreCase))
        {
            return TaxMode.Exclusive;
        }

        if (value.Trim().Equals("inclusive", StringComparison.OrdinalIgnoreCase))
        {
            return TaxMode.Inclusive;
        }

        errors.Add(new FieldError("taxMode", "taxMode.invalid"));
        return TaxMode.Exclusive;
    }

    private static decimal? NormalizeRate(string? value, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new FieldError("defaultTaxRate", "defaultTaxRate.required"));
            return null;
        }

        if (!decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
        {
            errors.Add(new FieldError("defaultTaxRate", "defaultTaxRate.invalid"));
            return null;
        }

        var scale = (decimal.GetBits(rate)[3] >> 16) & 0x7F;
        if (scale > 6)
        {
            errors.Add(new FieldError("defaultTaxRate", "defaultTaxRate.scale"));
            return null;
        }

        if (rate < 0m || rate > 1m)
        {
            errors.Add(new FieldError("defaultTaxRate", "defaultTaxRate.range"));
            return null;
        }

        return rate;
    }
}

public static class EmailAddressValidator
{
    public const int MaxLength = 256;

    public static IReadOnlyList<FieldError> Validate(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return [new FieldError("email", "email.required")];
        }

        if (trimmed.Length > MaxLength || trimmed.Any(char.IsControl))
        {
            return [new FieldError("email", "email.invalid")];
        }

        try
        {
            var parsed = new MailAddress(trimmed);
            if (!string.Equals(parsed.Address, trimmed, StringComparison.Ordinal))
            {
                return [new FieldError("email", "email.invalid")];
            }
        }
        catch (FormatException)
        {
            return [new FieldError("email", "email.invalid")];
        }

        return [];
    }
}
