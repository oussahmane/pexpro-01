using Pexilio.Domain;

namespace Pexilio.Domain.Catalog;

public readonly record struct ProductDraft(
    string Sku,
    string SkuKey,
    string Name,
    string Unit,
    decimal UnitPrice,
    bool TrackStock,
    bool IsActive);

public static class CatalogValidator
{
    public const int SkuMaxLength = 64;
    public const int NameMaxLength = 200;
    public const int UnitMaxLength = 16;
    public const int CategoryNameMaxLength = 120;

    public static bool TryProduct(
        string? sku,
        string? name,
        string? unit,
        string? unitPrice,
        bool? trackStock,
        bool? isActive,
        out ProductDraft draft,
        out IReadOnlyList<FieldError> errors)
    {
        var found = new List<FieldError>();
        var normalizedSku = Required(sku, "sku", SkuMaxLength, found);
        var normalizedName = Required(name, "name", NameMaxLength, found);
        var normalizedUnit = Required(unit, "unit", UnitMaxLength, found);
        var priceOk = UnitPriceText.TryParse(unitPrice, out var price, out var priceCode);
        if (!priceOk)
        {
            found.Add(new FieldError("unitPrice", priceCode));
        }

        if (found.Count > 0 || normalizedSku is null || normalizedName is null || normalizedUnit is null)
        {
            draft = default;
            errors = found;
            return false;
        }

        draft = new ProductDraft(
            normalizedSku,
            Key(normalizedSku),
            normalizedName,
            normalizedUnit,
            price,
            trackStock ?? true,
            isActive ?? true);
        errors = found;
        return true;
    }

    public static bool TryCategoryName(string? name, out string normalized, out string key, out IReadOnlyList<FieldError> errors)
    {
        var found = new List<FieldError>();
        var value = Required(name, "name", CategoryNameMaxLength, found);
        if (value is null)
        {
            normalized = "";
            key = "";
            errors = found;
            return false;
        }

        normalized = value;
        key = Key(value);
        errors = found;
        return true;
    }

    public static string Key(string value) => value.ToUpperInvariant();

    private static string? Required(string? value, string field, int maxLength, List<FieldError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new FieldError(field, $"{field}.required"));
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors.Add(new FieldError(field, $"{field}.length"));
            return null;
        }

        return trimmed;
    }
}
