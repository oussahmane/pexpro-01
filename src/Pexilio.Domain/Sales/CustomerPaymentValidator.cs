using Pexilio.Domain.Stock;

namespace Pexilio.Domain.Sales;

public readonly record struct CustomerPaymentDraft(string Method, decimal Amount, string? Reference, string? Notes);

public static class CustomerPaymentValidator
{
    public const int ReferenceMaxLength = 64;

    public const int NotesMaxLength = 500;

    public static bool TryNormalize(
        string? methodText,
        string? amountText,
        string? referenceText,
        string? notesText,
        out CustomerPaymentDraft draft,
        out IReadOnlyList<FieldError> errors)
    {
        var found = new List<FieldError>();
        var method = methodText?.Trim().ToLowerInvariant() ?? "";
        if (!SaleRules.PaymentMethods.Contains(method))
        {
            found.Add(new FieldError("method", "payment.method"));
        }

        var amountOk = QuantityText.TryParse(amountText, "payment", false, out var amount, out var amountCode);
        if (!amountOk)
        {
            found.Add(new FieldError("amount", amountCode));
        }
        else if (amount <= 0m)
        {
            found.Add(new FieldError("amount", "payment.positive"));
        }

        var reference = Optional(referenceText, "reference", ReferenceMaxLength, found);
        var notes = Optional(notesText, "notes", NotesMaxLength, found);
        if (found.Count > 0 || !amountOk)
        {
            draft = default;
            errors = found;
            return false;
        }

        draft = new CustomerPaymentDraft(method, amount, reference, notes);
        errors = found;
        return true;
    }

    private static string? Optional(string? value, string field, int maxLength, List<FieldError> found)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            found.Add(new FieldError(field, field + ".length"));
            return null;
        }

        return trimmed;
    }
}
