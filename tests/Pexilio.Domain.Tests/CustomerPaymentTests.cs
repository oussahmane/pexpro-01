using Pexilio.Domain.Sales;

namespace Pexilio.Domain.Tests;

public sealed class CustomerPaymentTests
{
    [Fact]
    public void A_customer_payment_accepts_one_positive_amount_and_optional_text()
    {
        var saved = CustomerPaymentValidator.TryNormalize(" Transfer ", "4.0000", "  VIR-1 ", "  ", out var draft, out var errors);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal("transfer", draft.Method);
        Assert.Equal(4.0000m, draft.Amount);
        Assert.Equal("VIR-1", draft.Reference);
        Assert.Null(draft.Notes);
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("transfer")]
    [InlineData("card")]
    [InlineData("other")]
    public void Each_supported_method_is_accepted(string method)
    {
        var saved = CustomerPaymentValidator.TryNormalize(method, "1.0000", null, null, out var draft, out var errors);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal(method, draft.Method);
    }

    [Fact]
    public void An_invalid_zero_negative_or_unknown_payment_is_rejected()
    {
        var unknown = CustomerPaymentValidator.TryNormalize("cheque", "1.0000", null, null, out _, out var methodErrors);
        var zero = CustomerPaymentValidator.TryNormalize("cash", "0.0000", null, null, out _, out var zeroErrors);
        var negative = CustomerPaymentValidator.TryNormalize("cash", "-1.0000", null, null, out _, out var signErrors);
        var longReference = CustomerPaymentValidator.TryNormalize("cash", "1.0000", new string('a', 65), null, out _, out var referenceErrors);

        Assert.False(unknown);
        Assert.Contains(methodErrors, error => error.Code == "payment.method");
        Assert.False(zero);
        Assert.Contains(zeroErrors, error => error.Code == "payment.positive");
        Assert.False(negative);
        Assert.Contains(signErrors, error => error.Code == "payment.sign");
        Assert.False(longReference);
        Assert.Contains(referenceErrors, error => error.Code == "reference.length");
    }

    [Fact]
    public void A_settled_sale_and_an_amount_above_the_outstanding_balance_are_rejected()
    {
        Assert.Null(SaleCollection.Problem(customerLinked: true, outstanding: 6.0000m, amount: 6.0000m));
        Assert.Null(SaleCollection.Problem(customerLinked: true, outstanding: 6.0000m, amount: 4.0000m));
        Assert.Equal(SaleCollection.Exceeds, SaleCollection.Problem(customerLinked: true, outstanding: 6.0000m, amount: 6.0001m));
        Assert.Equal(SaleCollection.Settled, SaleCollection.Problem(customerLinked: true, outstanding: 0m, amount: 1m));
        Assert.Equal(SaleCollection.CustomerRequired, SaleCollection.Problem(customerLinked: false, outstanding: 10m, amount: 1m));
    }

    [Fact]
    public void Receipt_numbers_keep_six_digits_and_widen_after_999999()
    {
        Assert.Equal("R-000001", PaymentReceiptNumbers.Format(1));
        Assert.Equal("R-000042", PaymentReceiptNumbers.Format(42));
        Assert.Equal("R-999999", PaymentReceiptNumbers.Format(999_999));
        Assert.Equal("R-1000000", PaymentReceiptNumbers.Format(1_000_000));
    }
}
