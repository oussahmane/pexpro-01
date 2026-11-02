using Pexilio.Domain.Catalog;
using Pexilio.Domain.Company;
using Pexilio.Domain.Purchasing;

namespace Pexilio.Domain.Customers;

public readonly record struct CustomerDraft(
    string Type,
    string Name,
    string NameKey,
    string? Phone,
    string? Address,
    string? Notes,
    string? Rc,
    string? Nif,
    string? Nis,
    string? Ai,
    decimal? CreditLimit,
    bool? IsActive);

public static class CustomerValidator
{
    public const int NameMaxLength = 160;

    public const int PhoneMaxLength = 40;

    public const int AddressMaxLength = 300;

    public const int NotesMaxLength = 500;

    public static bool TryCustomer(
        string? type,
        string? name,
        string? phone,
        string? address,
        string? notes,
        string? rc,
        string? nif,
        string? nis,
        string? ai,
        string? creditLimit,
        bool? isActive,
        out CustomerDraft draft,
        out IReadOnlyList<FieldError> errors)
    {
        var found = new List<FieldError>();
        var normalizedType = type?.Trim().ToLowerInvariant() ?? "";
        if (normalizedType.Length == 0)
        {
            found.Add(new FieldError("type", "type.required"));
        }
        else if (!CustomerTypes.All.Contains(normalizedType))
        {
            found.Add(new FieldError("type", "type.invalid"));
        }

        var normalizedName = Required(name, "name", found);
        var normalizedPhone = Optional(phone, "phone", PhoneMaxLength, found);
        var normalizedAddress = Optional(address, "address", AddressMaxLength, found);
        var normalizedNotes = Optional(notes, "notes", NotesMaxLength, found);
        var normalizedRc = Optional(rc, "rc", CompanyProfileValidator.IdentifierMaxLength, found);
        var normalizedNif = Optional(nif, "nif", CompanyProfileValidator.IdentifierMaxLength, found);
        var normalizedNis = Optional(nis, "nis", CompanyProfileValidator.IdentifierMaxLength, found);
        var normalizedAi = Optional(ai, "ai", CompanyProfileValidator.IdentifierMaxLength, found);
        if (normalizedPhone is not null && normalizedPhone.Any(char.IsControl))
        {
            found.Add(new FieldError("phone", "phone.invalid"));
        }

        decimal? limit = null;
        if (!string.IsNullOrWhiteSpace(creditLimit))
        {
            if (!UnitPriceText.TryParse(creditLimit, out var parsedLimit, out var limitCode))
            {
                found.Add(new FieldError("creditLimit", limitCode.Replace("unitPrice", "creditLimit", StringComparison.Ordinal)));
            }
            else
            {
                limit = parsedLimit;
            }
        }

        var identifiers = new[] { normalizedRc, normalizedNif, normalizedNis, normalizedAi };
        if (normalizedType == CustomerTypes.Individual && identifiers.Any(value => value is not null))
        {
            found.Add(new FieldError("type", "customer.identifier"));
        }

        if (found.Count > 0 || normalizedName is null || normalizedType.Length == 0 || !CustomerTypes.All.Contains(normalizedType))
        {
            draft = default;
            errors = found;
            return false;
        }

        draft = new CustomerDraft(
            normalizedType,
            normalizedName,
            PurchasingValidator.Key(normalizedName),
            normalizedPhone,
            normalizedAddress,
            normalizedNotes,
            normalizedRc,
            normalizedNif,
            normalizedNis,
            normalizedAi,
            limit,
            isActive);
        errors = found;
        return true;
    }

    private static string? Required(string? value, string field, List<FieldError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new FieldError(field, $"{field}.required"));
            return null;
        }

        if (trimmed.Length > NameMaxLength || PurchasingValidator.Key(trimmed).Length > NameMaxLength)
        {
            errors.Add(new FieldError(field, $"{field}.length"));
            return null;
        }

        return trimmed;
    }

    private static string? Optional(string? value, string field, int maxLength, List<FieldError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
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
