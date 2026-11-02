using Pexilio.Domain.Catalog;
using Pexilio.Domain.Sales;

namespace Pexilio.Domain.Tests;

public sealed class PricingRulesTests
{
    [Fact]
    public void Percentage_money_rounds_half_away_from_zero_at_scale_4()
    {
        Assert.True(SaleRules.TryPercentAmount(10.0000m, 33.3333m, out var third));
        Assert.Equal(3.3333m, third);
        Assert.True(SaleRules.TryPercentAmount(0.0001m, 50.0000m, out var half));
        Assert.Equal(0.0001m, half);
        Assert.True(SaleRules.TryPercentAmount(20.0000m, 10.0000m, out var ten));
        Assert.Equal(2.0000m, ten);
        Assert.False(SaleRules.TryPercentAmount(10.0000m, 100.0001m, out _));
    }

    [Fact]
    public void Ticket_discount_uses_largest_remainder_and_breaks_ties_by_line_order()
    {
        Assert.True(SaleRules.TryAllocate([10.0000m, 20.0000m, 30.0000m], 1.0000m, out var rounded));
        Assert.Equal([0.1667m, 0.3333m, 0.5000m], rounded);
        Assert.Equal(1.0000m, rounded.Sum());

        Assert.True(SaleRules.TryAllocate([10.0000m, 10.0000m, 10.0000m], 0.0002m, out var tied));
        Assert.Equal([0.0001m, 0.0001m, 0.0000m], tied);

        Assert.True(SaleRules.TryAllocate([9.0000m, 20.0000m, 30.0000m], 1.0000m, out var mixed));
        Assert.Equal([0.1525m, 0.3390m, 0.5085m], mixed);
        Assert.True(SaleRules.TryAllocate([5.0000m, 0.0000m], 1.0000m, out var zeroLine));
        Assert.Equal([1.0000m, 0.0000m], zeroLine);
        Assert.False(SaleRules.TryAllocate([10.0000m], 10.0001m, out _));
    }

    [Fact]
    public void A_missing_minimum_keeps_a_cashier_at_the_selected_price_and_records_an_administrator_override()
    {
        Assert.False(SaleRules.NetBelowFloor(10.0000m, 1.0000m, 10.0000m));
        Assert.True(SaleRules.NetBelowFloor(9.9999m, 1.0000m, 10.0000m));
        Assert.False(SaleRules.NetBelowFloor(8.0000m, 1.0000m, 8.0000m));
        Assert.True(SaleRules.TryGross(1.0000m, 10.0000m, out var floor));
        Assert.Equal(10.0000m, floor);
        Assert.False(SaleRules.TryGross(1.0001m, 1.0001m, out _));
    }

    [Fact]
    public void Fixed_and_percentage_discounts_must_match_the_line_math()
    {
        var product = Guid.NewGuid();
        var percent = SaleValidator.TryCashSale(
            [new SaleLineInput(product, "1.0000", "10.0000", "9.0000", "retail", "1.0000", "percent", "10.0000")],
            "8.1000",
            [("cash", "8.1000")],
            "0.9000",
            "percent",
            "10.0000",
            out var draft,
            out var errors);

        Assert.True(percent);
        Assert.Empty(errors);
        Assert.True(draft.Extended);
        Assert.Equal(0.9000m, draft.DiscountAmount);
        Assert.Equal(1.0000m, Assert.Single(draft.Lines).LineDiscount);

        var legacy = SaleValidator.TryCashSale(
            [(product, "2.0000", "10.5000", "21.0000")],
            "21.0000",
            [("cash", "21.0000")],
            out var old,
            out _);
        Assert.True(legacy);
        Assert.False(old.Extended);
        Assert.Equal(0m, old.DiscountAmount);

        var forged = SaleValidator.TryCashSale(
            [new SaleLineInput(product, "1.0000", "10.0000", "8.0000", "retail", "2.0000", "percent", "10.0000")],
            "8.0000",
            [("cash", "8.0000")],
            null,
            null,
            null,
            out _,
            out var forgedErrors);
        Assert.False(forged);
        Assert.Contains(forgedErrors, error => error.Code == "discount.mismatch");
    }

    [Fact]
    public void A_minimum_cannot_sit_above_retail_or_wholesale()
    {
        Assert.True(CatalogValidator.TryPriceBounds(10m, 8m, 8m, out _));
        Assert.True(CatalogValidator.TryPriceBounds(10m, null, null, out _));
        Assert.False(CatalogValidator.TryPriceBounds(10m, 8m, 9m, out var wholesale));
        Assert.Equal("minimum.above_wholesale", wholesale);
        Assert.False(CatalogValidator.TryPriceBounds(10m, null, 11m, out var retail));
        Assert.Equal("minimum.above_price", retail);
    }

    [Fact]
    public void A_fractional_floor_compares_scaled_integers_and_the_charged_gross_stays_exact()
    {
        Assert.False(SaleRules.TryGross(1.5000m, 0.3333m, out _));
        Assert.False(SaleRules.NetBelowFloor(15.0000m, 1.5000m, 0.3333m));
        Assert.False(SaleRules.NetBelowFloor(0.5000m, 1.5000m, 0.3333m));
        Assert.True(SaleRules.NetBelowFloor(0.4999m, 1.5000m, 0.3333m));

        Assert.False(SaleRules.TryGross(1.0001m, 1.0001m, out _));
        Assert.False(SaleRules.NetBelowFloor(2.0002m, 1.0001m, 1.0001m));

        var product = Guid.NewGuid();
        var charged = SaleValidator.TryCashSale(
            [(product, "1.0001", "1.0001", "1.0002")],
            "1.0002",
            [("cash", "1.0002")],
            out _,
            out var errors);

        Assert.False(charged);
        Assert.Contains(errors, error => error.Code == "line.scale");
    }

    [Theory]
    [InlineData(null, "discountRate.required")]
    [InlineData("", "discountRate.required")]
    [InlineData("   ", "discountRate.required")]
    [InlineData("nope", "discountRate.invalid")]
    [InlineData("10.12345", "discountRate.scale")]
    [InlineData("-0.0001", "discount.rate")]
    [InlineData("101", "discount.rate")]
    [InlineData("100.0001", "discount.rate")]
    [InlineData("1000000000000000", "discount.rate")]
    public void An_invalid_discount_rate_never_reports_a_unit_price_error(string? rate, string code)
    {
        Assert.False(SaleRules.TryRate(rate, out _, out var parsed));
        Assert.Equal(code, parsed);
        Assert.DoesNotContain("unitPrice", parsed, StringComparison.Ordinal);

        var product = Guid.NewGuid();
        var line = SaleValidator.TryCashSale(
            [new SaleLineInput(product, "1.0000", "10.0000", "9.0000", "retail", "1.0000", "percent", rate)],
            "9.0000",
            [("cash", "9.0000")],
            null,
            null,
            null,
            out _,
            out var lineErrors);
        var ticket = SaleValidator.TryCashSale(
            [new SaleLineInput(product, "1.0000", "10.0000", "10.0000", "retail", "0.0000", "none", null)],
            "9.0000",
            [("cash", "9.0000")],
            "1.0000",
            "percent",
            rate,
            out _,
            out var ticketErrors);

        Assert.False(line);
        Assert.False(ticket);
        Assert.Contains(lineErrors, error => error.Field == "discountRate" && error.Code == code);
        Assert.Contains(ticketErrors, error => error.Field == "discountRate" && error.Code == code);
        Assert.DoesNotContain(lineErrors, error => error.Field == "unitPrice" || error.Code.Contains("unitPrice", StringComparison.Ordinal));
        Assert.DoesNotContain(ticketErrors, error => error.Field == "unitPrice" || error.Code.Contains("unitPrice", StringComparison.Ordinal));
    }

    [Fact]
    public void A_discount_rate_from_zero_through_one_hundred_is_accepted()
    {
        Assert.True(SaleRules.TryRate("0", out var zero, out var zeroCode));
        Assert.Equal(0m, zero);
        Assert.Equal("", zeroCode);
        Assert.True(SaleRules.TryRate("100", out var hundred, out var hundredCode));
        Assert.Equal(100m, hundred);
        Assert.Equal("", hundredCode);
        Assert.True(SaleRules.TryRate("100.0000", out _, out _));
    }
}
