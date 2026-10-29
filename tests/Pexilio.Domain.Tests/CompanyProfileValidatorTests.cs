using Pexilio.Domain.Company;

namespace Pexilio.Domain.Tests;

public sealed class CompanyProfileValidatorTests
{
    [Fact]
    public void Uses_DZD_and_Algiers_when_those_fields_are_omitted()
    {
        var input = Valid();
        input.CurrencyCode = null;
        input.CurrencyScale = null;
        input.TimeZoneId = " ";
        input.TaxMode = null;

        var ok = CompanyProfileValidator.TryNormalize(input, out var profile, out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.NotNull(profile);
        Assert.Equal("DZD", profile.CurrencyCode);
        Assert.Equal(2, profile.CurrencyScale);
        Assert.Equal("Africa/Algiers", profile.TimeZoneId);
        Assert.Equal(TaxMode.Exclusive, profile.TaxMode);
        Assert.Equal(0.19m, profile.DefaultTaxRate);
        Assert.Null(profile.Rc);
    }

    [Theory]
    [InlineData("1.5", "defaultTaxRate.range")]
    [InlineData("-0.01", "defaultTaxRate.range")]
    [InlineData("0.1234567", "defaultTaxRate.scale")]
    [InlineData("nope", "defaultTaxRate.invalid")]
    [InlineData("", "defaultTaxRate.required")]
    public void Rejects_a_tax_rate_that_cannot_be_stored(string rate, string code)
    {
        var input = Valid();
        input.DefaultTaxRate = rate;

        var ok = CompanyProfileValidator.TryNormalize(input, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, error => error.Field == "defaultTaxRate" && error.Code == code);
    }

    [Fact]
    public void Rejects_an_unknown_time_zone_and_a_short_currency_code()
    {
        var input = Valid();
        input.TimeZoneId = "Not/AZone";
        input.CurrencyCode = "dz";

        var ok = CompanyProfileValidator.TryNormalize(input, out _, out var errors);

        Assert.False(ok);
        Assert.Contains(errors, error => error.Code == "timeZoneId.invalid");
        Assert.Contains(errors, error => error.Code == "currencyCode.invalid");
    }

    private static CompanyInput Valid() => new()
    {
        LegalName = "  Société Démo  ",
        DefaultTaxRate = "0.19",
        Rc = " ",
    };
}
