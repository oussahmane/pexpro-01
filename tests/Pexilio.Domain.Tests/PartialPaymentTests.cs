using Pexilio.Domain;
using Pexilio.Domain.Sales;

namespace Pexilio.Domain.Tests;

public sealed class PartialPaymentTests
{
    [Fact]
    public void A_customer_sale_accepts_a_full_partial_or_empty_payment()
    {
        var product = Guid.NewGuid();
        var full = Accept(product, "10.0000", [("cash", "10.0000")]);
        var partial = Accept(product, "10.0000", [("cash", "4.0000")]);
        var unpaid = Accept(product, "10.0000", []);

        Assert.Equal(10.0000m, Assert.Single(full.Payments).Amount);
        Assert.Equal(4.0000m, Assert.Single(partial.Payments).Amount);
        Assert.Empty(unpaid.Payments);
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("transfer")]
    [InlineData("card")]
    [InlineData("other")]
    public void A_customer_sale_accepts_one_known_payment_method(string method)
    {
        var draft = Accept(Guid.NewGuid(), "10.0000", [(method, "10.0000")]);

        Assert.Equal(method, Assert.Single(draft.Payments).Method);
    }

    [Fact]
    public void A_walk_in_sale_still_requires_one_full_cash_payment()
    {
        var product = Guid.NewGuid();
        var saved = SaleValidator.TryCashSale(
            [(product, "1.0000", "10.0000", "10.0000")],
            "10.0000",
            [("cash", "10.0000")],
            out var draft,
            out var errors);
        var partial = Reject(false, [("cash", "4.0000")]);
        var unpaid = Reject(false, []);
        var transfer = Reject(false, [("transfer", "10.0000")]);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal(SaleRules.CashMethod, Assert.Single(draft.Payments).Method);
        Assert.Equal(10.0000m, draft.Payments[0].Amount);
        Assert.Contains(partial, error => error.Code == "payment.total");
        Assert.Contains(unpaid, error => error.Code == "payments.required");
        Assert.Contains(transfer, error => error.Code == "payment.method");
    }

    [Fact]
    public void Two_payments_a_non_positive_amount_and_an_amount_above_the_total_are_rejected()
    {
        var split = Reject(true, [("cash", "6.0000"), ("transfer", "4.0000")]);
        var zero = Reject(true, [("cash", "0.0000")]);
        var negative = Reject(true, [("cash", "-1.0000")]);
        var above = Reject(true, [("card", "10.0001")]);
        var zeroTotal = SaleValidator.TryCashSale(
            Lines("0.0000"),
            "0.0000",
            [],
            null,
            null,
            null,
            true,
            out _,
            out var totalErrors);

        Assert.Contains(new FieldError("payments", "payments.single"), split);
        Assert.Contains(new FieldError("payment", "payment.positive"), zero);
        Assert.Contains(new FieldError("payment", "payment.sign"), negative);
        Assert.Contains(new FieldError("payments", "payment.total"), above);
        Assert.False(zeroTotal);
        Assert.Contains(new FieldError("total", "total.positive"), totalErrors);
    }

    [Fact]
    public void An_empty_missing_or_inactive_customer_is_rejected_and_an_active_customer_is_accepted()
    {
        var customerId = Guid.NewGuid();

        Assert.Equal(SaleCustomerLink.Invalid, SaleCustomerLink.Problem(Guid.Empty, found: false, active: false));
        Assert.Equal(ErrorCodes.CustomerNotFound, SaleCustomerLink.Problem(customerId, found: false, active: true));
        Assert.Equal(ErrorCodes.CustomerInactive, SaleCustomerLink.Problem(customerId, found: true, active: false));
        Assert.Null(SaleCustomerLink.Problem(customerId, found: true, active: true));
        Assert.Null(SaleCustomerLink.Problem(null, found: false, active: false));
    }

    [Fact]
    public void Payment_state_is_derived_from_the_outstanding_amount()
    {
        Assert.True(SaleBalances.TryDerive(10.0000m, 0m, 10.0000m, out var paid));
        Assert.True(SaleBalances.TryDerive(10.0000m, 0m, 4.0000m, out var partial));
        Assert.True(SaleBalances.TryDerive(10.0000m, 0m, 0m, out var unpaid));
        Assert.True(SaleBalances.TryDerive(10.0000m, 10.0000m, 0m, out var returned));

        Assert.Equal(SalePaymentStates.Paid, paid.State);
        Assert.Equal(0.0000m, paid.Outstanding);
        Assert.Equal(SalePaymentStates.Partial, partial.State);
        Assert.Equal(6.0000m, partial.Outstanding);
        Assert.Equal(SalePaymentStates.Unpaid, unpaid.State);
        Assert.Equal(10.0000m, unpaid.Outstanding);
        Assert.Equal(SalePaymentStates.Paid, returned.State);
        Assert.Equal(0.0000m, returned.Net);
        Assert.False(SaleBalances.TryDerive(10.0000m, 0m, 10.0001m, out _));
    }

    private static SaleDraft Accept(Guid product, string total, (string? Method, string? Amount)[] payments)
    {
        var saved = SaleValidator.TryCashSale(
            [new SaleLineInput(product, "1.0000", total, total, null, null, null, null)],
            total,
            payments,
            null,
            null,
            null,
            true,
            out var draft,
            out var errors);

        Assert.True(saved);
        Assert.Empty(errors);
        return draft;
    }

    private static IReadOnlyList<FieldError> Reject(bool customerLinked, (string? Method, string? Amount)[] payments)
    {
        var saved = SaleValidator.TryCashSale(
            Lines("10.0000"),
            "10.0000",
            payments,
            null,
            null,
            null,
            customerLinked,
            out _,
            out var errors);

        Assert.False(saved);
        return errors;
    }

    private static SaleLineInput[] Lines(string total) =>
    [
        new(Guid.NewGuid(), "1.0000", total, total, null, null, null, null),
    ];
}
