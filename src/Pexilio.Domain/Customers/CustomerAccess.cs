namespace Pexilio.Domain.Customers;

public readonly record struct CustomerStored(
    string Type,
    string? Rc,
    string? Nif,
    string? Nis,
    string? Ai,
    decimal? CreditLimit,
    bool IsActive);

public static class CustomerAccess
{
    public static bool TryCreate(
        bool administrator,
        CustomerDraft requested,
        out CustomerDraft effective,
        out IReadOnlyList<FieldError> errors)
    {
        if (administrator)
        {
            effective = requested with { IsActive = requested.IsActive ?? true };
            errors = [];
            return true;
        }

        var found = Restricted(requested, null);
        if (found.Count > 0)
        {
            effective = default;
            errors = found;
            return false;
        }

        effective = requested with { IsActive = true };
        errors = found;
        return true;
    }

    public static bool TryUpdate(
        bool administrator,
        CustomerDraft requested,
        CustomerStored current,
        out CustomerDraft effective,
        out IReadOnlyList<FieldError> errors)
    {
        if (administrator)
        {
            effective = requested with { IsActive = requested.IsActive ?? current.IsActive };
            errors = [];
            return true;
        }

        var found = Restricted(requested, current);
        if (found.Count > 0)
        {
            effective = default;
            errors = found;
            return false;
        }

        effective = requested with
        {
            Type = current.Type,
            Rc = current.Rc,
            Nif = current.Nif,
            Nis = current.Nis,
            Ai = current.Ai,
            CreditLimit = current.CreditLimit,
            IsActive = current.IsActive,
        };
        errors = found;
        return true;
    }

    private static List<FieldError> Restricted(CustomerDraft requested, CustomerStored? current)
    {
        var found = new List<FieldError>();
        if (current is CustomerStored stored && requested.Type != stored.Type)
        {
            found.Add(new FieldError("type", "customer.forbidden"));
        }

        Restrict(found, "rc", requested.Rc, current?.Rc);
        Restrict(found, "nif", requested.Nif, current?.Nif);
        Restrict(found, "nis", requested.Nis, current?.Nis);
        Restrict(found, "ai", requested.Ai, current?.Ai);
        if (requested.CreditLimit is not null && requested.CreditLimit != current?.CreditLimit)
        {
            found.Add(new FieldError("creditLimit", "customer.forbidden"));
        }

        var active = requested.IsActive ?? (current?.IsActive ?? true);
        if (active != (current?.IsActive ?? true))
        {
            found.Add(new FieldError("isActive", "customer.forbidden"));
        }

        return found;
    }

    private static void Restrict(List<FieldError> found, string field, string? requested, string? current)
    {
        if (requested is not null && requested != current)
        {
            found.Add(new FieldError(field, "customer.forbidden"));
        }
    }
}
