using Pexilio.Domain.Accounts;

namespace Pexilio.Domain.Tests;

public sealed class PasswordPolicyTests
{
    [Fact]
    public void Requires_at_least_12_characters()
    {
        Assert.Contains(PasswordPolicy.Validate("short-pass"), error => error.Code == "password.too_short");
        Assert.Empty(PasswordPolicy.Validate("long-enough-1"));
    }
}
