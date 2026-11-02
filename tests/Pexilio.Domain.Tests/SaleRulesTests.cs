using Pexilio.Domain;
using Pexilio.Domain.Sales;

namespace Pexilio.Domain.Tests;

public sealed class SaleRulesTests
{
    [Fact]
    public void Sale_numbers_keep_a_stable_prefix_and_width()
    {
        Assert.Equal("V-000001", SaleNumbers.Format(1));
        Assert.Equal("V-1000000", SaleNumbers.Format(1_000_000));
        Assert.Equal("AV-000042", SaleNumbers.FormatReturn(42));
    }

    [Fact]
    public void A_sale_movement_is_the_negative_of_the_sold_quantity()
    {
        Assert.Equal(-2.5000m, SaleRules.SaleDelta(2.5000m));
        Assert.Equal(2.5000m, SaleRules.ReturnDelta(2.5000m));
        Assert.False(SaleRules.OneProduct([Guid.Empty, Guid.Empty]));
        Assert.True(SaleRules.OneProduct([Guid.NewGuid(), Guid.NewGuid()]));
    }

    [Fact]
    public void Line_gross_stays_exact_at_scale_4()
    {
        Assert.True(SaleRules.TryGross(2.0000m, 10.5000m, out var gross));
        Assert.Equal(21.0000m, gross);
        Assert.False(SaleRules.TryGross(1.0001m, 1.0001m, out _));
        Assert.False(SaleRules.TryGross(0m, 10m, out _));
    }

    [Fact]
    public void A_cash_sale_accepts_matching_totals_and_one_line_per_product()
    {
        var product = Guid.NewGuid();
        var saved = SaleValidator.TryCashSale(
            [(product, "2.0000", "10.5000", "21.0000")],
            "21.0000",
            [("cash", "21.0000")],
            out var draft,
            out var errors);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal(21.0000m, draft.Total);
        Assert.Equal(SaleRules.CashMethod, Assert.Single(draft.Payments).Method);
    }

    [Fact]
    public void Duplicate_products_wrong_totals_and_non_cash_payments_are_rejected()
    {
        var product = Guid.NewGuid();
        var duplicate = SaleValidator.TryCashSale(
            [(product, "1.0000", "10.0000", "10.0000"), (product, "1.0000", "10.0000", "10.0000")],
            "20.0000",
            [("cash", "20.0000")],
            out _,
            out var duplicateErrors);
        var mismatch = SaleValidator.TryCashSale(
            [(product, "2.0000", "10.0000", "15.0000")],
            "15.0000",
            [("cash", "15.0000")],
            out _,
            out var mismatchErrors);
        var credit = SaleValidator.TryCashSale(
            [(product, "1.0000", "10.0000", "10.0000")],
            "10.0000",
            [("transfer", "10.0000")],
            out _,
            out var creditErrors);
        var shortPayment = SaleValidator.TryCashSale(
            [(product, "1.0000", "10.0000", "10.0000")],
            "10.0000",
            [("cash", "4.0000")],
            out _,
            out var paymentErrors);

        Assert.False(duplicate);
        Assert.Contains(duplicateErrors, error => error.Code == "lines.duplicate");
        Assert.False(mismatch);
        Assert.Contains(mismatchErrors, error => error.Code == "lineTotal.mismatch");
        Assert.False(credit);
        Assert.Contains(creditErrors, error => error.Code == "payment.method");
        Assert.False(shortPayment);
        Assert.Contains(paymentErrors, error => error.Code == "payment.total");
    }
}
