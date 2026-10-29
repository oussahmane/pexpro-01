using Pexilio.Domain.Stock;

namespace Pexilio.Domain.Tests;

public sealed class QuantityTextTests
{
    [Theory]
    [InlineData("1.5000", "1.5000")]
    [InlineData("1.5", "1.5000")]
    [InlineData("-1.25", "-1.2500")]
    [InlineData("0", "0.0000")]
    [InlineData("999999999999999.9999", "999999999999999.9999")]
    public void Accepts_scale_up_to_four_places(string text, string formatted)
    {
        var parsed = QuantityText.TryParse(text, "quantity", true, out var value, out var code);

        Assert.True(parsed);
        Assert.Equal("", code);
        Assert.Equal(formatted, QuantityText.Format(value));
    }

    [Theory]
    [InlineData("1.23456", "quantity.scale")]
    [InlineData("1.23000", "quantity.scale")]
    [InlineData("1e-1", "quantity.invalid")]
    [InlineData("1,000.0000", "quantity.invalid")]
    [InlineData("", "quantity.required")]
    [InlineData("1000000000000000.0000", "quantity.range")]
    public void Rejects_values_that_would_need_rounding_or_are_not_decimal_quantities(string text, string code)
    {
        var parsed = QuantityText.TryParse(text, "quantity", true, out _, out var actual);

        Assert.False(parsed);
        Assert.Equal(code, actual);
    }

    [Fact]
    public void Rejects_a_negative_quantity_when_the_operation_requires_a_positive_sign()
    {
        var parsed = QuantityText.TryParse("-1.0000", "quantity", false, out _, out var code);

        Assert.False(parsed);
        Assert.Equal("quantity.sign", code);
    }

    [Fact]
    public void Opening_and_receipt_must_be_positive_and_adjustment_must_be_non_zero()
    {
        Assert.Equal("quantity.zero", DeltaCode(StockMovementTypes.Opening, 0m));
        Assert.Equal("quantity.positive", DeltaCode(StockMovementTypes.Receipt, -1m));
        Assert.Equal("quantity.zero", DeltaCode(StockMovementTypes.Adjustment, 0m));
        Assert.Equal("", DeltaCode(StockMovementTypes.Adjustment, -1m));
        Assert.Equal("", DeltaCode(StockMovementTypes.Opening, 0.0001m));
    }

    [Fact]
    public void Below_minimum_is_strict_and_equality_is_at_minimum()
    {
        Assert.True(StockRules.BelowMinimum(1m, 2m));
        Assert.False(StockRules.AtMinimum(1m, 2m));
        Assert.False(StockRules.BelowMinimum(2m, 2m));
        Assert.True(StockRules.AtMinimum(2m, 2m));
        Assert.False(StockRules.BelowMinimum(2m, null));
        Assert.False(StockRules.AtMinimum(2m, null));
    }

    [Fact]
    public void A_manual_reason_is_required()
    {
        Assert.False(StockRules.TryReason("   ", out _, out var missing));
        Assert.Equal("reason.required", missing);
        Assert.True(StockRules.TryReason(" Inventaire ", out var reason, out var code));
        Assert.Equal("Inventaire", reason);
        Assert.Equal("", code);
    }

    private static string DeltaCode(string movementType, decimal quantity)
    {
        StockRules.TryDelta(movementType, quantity, out var code);
        return code;
    }
}
