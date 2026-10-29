using System.Text;
using System.Text.RegularExpressions;
using Pexilio.Domain.Stock;

namespace Pexilio.Domain.Purchasing;

public readonly record struct SupplierDraft(
    string Name,
    string NameKey,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? TaxId,
    string? Notes,
    bool IsActive);

public readonly record struct DraftLine(Guid ProductId, decimal Quantity, decimal UnitCost);

public readonly record struct PurchaseDraftInput(
    string? SupplierDocumentRef,
    string? SupplierDocumentKey,
    string? Notes,
    IReadOnlyList<DraftLine> Lines);

public static partial class PurchasingValidator
{
    public const int SupplierNameMaxLength = 160;

    public const int ContactNameMaxLength = 120;

    public const int PhoneMaxLength = 40;

    public const int EmailMaxLength = 160;

    public const int AddressMaxLength = 300;

    public const int TaxIdMaxLength = 40;

    public const int NotesMaxLength = 500;

    public const int DocumentRefMaxLength = 40;

    public const int NumberMaxLength = 32;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[\p{L}\p{N} /._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentRefPattern();

    public static bool TrySupplier(
        string? name,
        string? contactName,
        string? phone,
        string? email,
        string? address,
        string? taxId,
        string? notes,
        bool? isActive,
        out SupplierDraft draft,
        out IReadOnlyList<FieldError> errors)
    {
        var found = new List<FieldError>();
        var normalizedName = Required(name, "name", SupplierNameMaxLength, found);
        var contact = Optional(contactName, "contactName", ContactNameMaxLength, found);
        var normalizedPhone = Optional(phone, "phone", PhoneMaxLength, found);
        var normalizedEmail = Optional(email, "email", EmailMaxLength, found);
        var normalizedAddress = Optional(address, "address", AddressMaxLength, found);
        var normalizedTaxId = Optional(taxId, "taxId", TaxIdMaxLength, found);
        var normalizedNotes = Optional(notes, "notes", NotesMaxLength, found);
        if (normalizedPhone is not null && normalizedPhone.Any(char.IsControl))
        {
            found.Add(new FieldError("phone", "phone.invalid"));
        }

        if (normalizedEmail is not null && !EmailPattern().IsMatch(normalizedEmail))
        {
            found.Add(new FieldError("email", "email.invalid"));
        }

        if (found.Count > 0 || normalizedName is null)
        {
            draft = default;
            errors = found;
            return false;
        }

        draft = new SupplierDraft(
            normalizedName,
            Key(normalizedName),
            contact,
            normalizedPhone,
            normalizedEmail,
            normalizedAddress,
            normalizedTaxId,
            normalizedNotes,
            isActive ?? true);
        errors = found;
        return true;
    }

    public static bool TryDraft(
        string? supplierDocumentRef,
        string? notes,
        IReadOnlyList<(Guid ProductId, string? Quantity, string? UnitCost)>? lines,
        out PurchaseDraftInput draft,
        out IReadOnlyList<FieldError> errors)
    {
        var found = new List<FieldError>();
        var document = TryDocumentRef(supplierDocumentRef, found);
        var normalizedNotes = Optional(notes, "notes", NotesMaxLength, found);
        var parsedLines = new List<DraftLine>();
        var seen = new HashSet<Guid>();
        var source = lines ?? [];
        for (var index = 0; index < source.Count; index++)
        {
            var (productId, quantityText, unitCostText) = source[index];
            var prefix = $"lines[{index}]";
            if (productId == Guid.Empty)
            {
                found.Add(new FieldError($"{prefix}.productId", "lines.product"));
            }
            else if (!seen.Add(productId))
            {
                found.Add(new FieldError($"{prefix}.productId", "lines.duplicate"));
            }

            if (!QuantityText.TryParse(quantityText, "quantity", false, out var quantity, out var quantityCode)
                || quantity <= 0m)
            {
                found.Add(new FieldError($"{prefix}.quantity", quantity <= 0m && quantityCode.Length == 0 ? "quantity.positive" : quantityCode));
            }

            if (!QuantityText.TryParse(unitCostText, "unitCost", false, out var unitCost, out var costCode))
            {
                found.Add(new FieldError($"{prefix}.unitCost", costCode));
                unitCost = 0m;
            }

            if (productId != Guid.Empty && seen.Count > 0 && quantity > 0m && costCode.Length == 0)
            {
                parsedLines.Add(new DraftLine(productId, quantity, unitCost));
            }
        }

        if (found.Count > 0 || document.Failed)
        {
            draft = default;
            errors = found;
            return false;
        }

        draft = new PurchaseDraftInput(document.Reference, document.Key, normalizedNotes, parsedLines);
        errors = found;
        return true;
    }

    private static readonly char[] Ligatures = ['\uFB00', '\uFB01', '\uFB02', '\uFB03', '\uFB04', '\uFB05', '\uFB06'];

    public static string Key(string value)
    {
        var upper = value.ToUpperInvariant();
        if (upper.IndexOfAny(Ligatures) < 0)
        {
            return upper;
        }

        var expanded = new StringBuilder(upper.Length);
        foreach (var character in upper)
        {
            switch (character)
            {
                case '\uFB00':
                    expanded.Append("FF");
                    break;
                case '\uFB01':
                    expanded.Append("FI");
                    break;
                case '\uFB02':
                    expanded.Append("FL");
                    break;
                case '\uFB03':
                    expanded.Append("FFI");
                    break;
                case '\uFB04':
                    expanded.Append("FFL");
                    break;
                case '\uFB05' or '\uFB06':
                    expanded.Append("ST");
                    break;
                default:
                    expanded.Append(character);
                    break;
            }
        }

        return expanded.ToString();
    }

    private static bool ExceedsStoredLength(string value, int maxLength) =>
        value.Length > maxLength || Key(value).Length > maxLength;

    private static (bool Failed, string? Reference, string? Key) TryDocumentRef(string? value, List<FieldError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (false, null, null);
        }

        if (ExceedsStoredLength(trimmed, DocumentRefMaxLength))
        {
            errors.Add(new FieldError("supplierDocumentRef", "supplierDocumentRef.length"));
            return (true, null, null);
        }

        if (!DocumentRefPattern().IsMatch(trimmed))
        {
            errors.Add(new FieldError("supplierDocumentRef", "supplierDocumentRef.invalid"));
            return (true, null, null);
        }

        return (false, trimmed, Key(trimmed));
    }

    private static string? Required(string? value, string field, int maxLength, List<FieldError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(new FieldError(field, $"{field}.required"));
            return null;
        }

        if (ExceedsStoredLength(trimmed, maxLength))
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
