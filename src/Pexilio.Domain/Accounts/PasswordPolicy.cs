using Pexilio.Domain;

namespace Pexilio.Domain.Accounts;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static IReadOnlyList<FieldError> Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return [new FieldError("password", "password.required")];
        }

        if (password.Length < MinimumLength)
        {
            return [new FieldError("password", "password.too_short")];
        }

        if (password.Length > MaximumLength)
        {
            return [new FieldError("password", "password.too_long")];
        }

        return [];
    }
}
