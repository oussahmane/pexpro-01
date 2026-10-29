using Pexilio.Domain.Catalog;

namespace Pexilio.Domain.Tests;

public sealed class CatalogTests
{
    [Theory]
    [InlineData("19.5000")]
    [InlineData("0.0001")]
    [InlineData("999999999999999.9999")]
    public void Unit_price_round_trips_as_a_four_decimal_string(string text)
    {
        Assert.True(UnitPriceText.TryParse(text, out var value, out _));
        Assert.Equal(text, UnitPriceText.Format(value));
    }

    [Theory]
    [InlineData("1.23456", "unitPrice.scale")]
    [InlineData("-0.0001", "unitPrice.range")]
    [InlineData("nope", "unitPrice.invalid")]
    [InlineData("", "unitPrice.required")]
    public void Unit_price_rejects_values_that_cannot_be_stored(string text, string code)
    {
        Assert.False(UnitPriceText.TryParse(text, out _, out var actual));
        Assert.Equal(code, actual);
    }

    [Fact]
    public void Moving_a_category_under_its_descendant_is_a_cycle()
    {
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?>
        {
            [root] = null,
            [child] = root,
        };

        Assert.True(CategoryGraph.CreatesCycle(root, child, id => parents[id]));
        Assert.False(CategoryGraph.CreatesCycle(child, null, id => parents[id]));
    }
}
