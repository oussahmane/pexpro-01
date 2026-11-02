using Pexilio.Domain;
using Pexilio.Domain.Purchasing;
using Pexilio.Domain.Stock;

namespace Pexilio.Domain.Tests;

public sealed class PurchaseReceiptRulesTests
{
    [Fact]
    public void Ordering_requires_a_draft_with_at_least_one_line()
    {
        Assert.True(PurchaseTransitions.CanOrder(PurchaseStatuses.Draft, 1));
        Assert.False(PurchaseTransitions.CanOrder(PurchaseStatuses.Draft, 0));
        Assert.False(PurchaseTransitions.CanOrder(PurchaseStatuses.Ordered, 1));
    }

    [Theory]
    [InlineData(PurchaseStatuses.Ordered, true)]
    [InlineData(PurchaseStatuses.PartiallyReceived, true)]
    [InlineData(PurchaseStatuses.Draft, false)]
    [InlineData(PurchaseStatuses.Received, false)]
    [InlineData(PurchaseStatuses.Cancelled, false)]
    public void Receiving_stays_on_an_open_order(string status, bool allowed)
    {
        Assert.Equal(allowed, PurchaseTransitions.CanReceive(status));
    }

    [Fact]
    public void Cancellation_is_only_an_ordered_purchase_with_no_receipt()
    {
        Assert.True(PurchaseTransitions.CanCancel(PurchaseStatuses.Ordered, false, false));
        Assert.False(PurchaseTransitions.CanCancel(PurchaseStatuses.Ordered, true, false));
        Assert.False(PurchaseTransitions.CanCancel(PurchaseStatuses.Ordered, false, true));
        Assert.False(PurchaseTransitions.CanCancel(PurchaseStatuses.PartiallyReceived, false, false));
        Assert.Equal(PurchaseStatuses.PartiallyReceived, PurchaseTransitions.AfterReceipt(false));
        Assert.Equal(PurchaseStatuses.Received, PurchaseTransitions.AfterReceipt(true));
    }

    [Fact]
    public void A_blank_receipt_quantity_is_omitted_and_zero_is_rejected()
    {
        var lines = new (Guid, string?)[]
        {
            (Guid.NewGuid(), "  "),
            (Guid.NewGuid(), "0.0000"),
            (Guid.NewGuid(), "1.5000"),
        };

        var saved = PurchasingValidator.TryReceipt("BL-9", lines, out var receipt, out var errors);

        Assert.False(saved);
        Assert.Contains(errors, error => error.Code == "quantity.positive");
        Assert.Null(receipt.DocumentRef);
    }

    [Fact]
    public void A_receipt_keeps_positive_lines_and_the_document_match_key()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var lines = new (Guid, string?)[]
        {
            (first, "1.2500"),
            (second, ""),
        };

        var saved = PurchasingValidator.TryReceipt("  bl-9  ", lines, out var receipt, out var errors);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal("bl-9", receipt.DocumentRef);
        Assert.Equal("BL-9", receipt.DocumentKey);
        var line = Assert.Single(receipt.Lines);
        Assert.Equal(first, line.PurchaseLineId);
        Assert.Equal(1.2500m, line.Quantity);
    }

    [Fact]
    public void Receipt_quantity_cannot_pass_the_remaining_amount()
    {
        Assert.True(PurchaseTransitions.WithinRemaining(1.5m, 2m));
        Assert.True(PurchaseTransitions.WithinRemaining(2m, 2m));
        Assert.False(PurchaseTransitions.WithinRemaining(2.0001m, 2m));
        Assert.False(PurchaseTransitions.WithinRemaining(0m, 2m));
    }

    [Fact]
    public void One_movement_per_product_records_the_balance_the_deferred_trigger_accepts()
    {
        var water = Guid.NewGuid();
        var flour = Guid.NewGuid();
        Assert.True(PurchaseTransitions.OneMovementPerProduct([water, flour]));
        Assert.False(PurchaseTransitions.OneMovementPerProduct([water, water]));

        var waterAfter = StockRules.NextBalance(0m, 2.0000m);
        var flourAfter = StockRules.NextBalance(1.5000m, 3.5000m);
        Assert.Equal(2.0000m, waterAfter);
        Assert.Equal(5.0000m, flourAfter);
        Assert.True(StockRules.DeferredTriggerAccepts([waterAfter], waterAfter));
        Assert.True(StockRules.DeferredTriggerAccepts([flourAfter], flourAfter));
    }

    [Fact]
    public void Two_movements_for_one_product_in_one_transaction_fail_stock_reconciliation()
    {
        var first = StockRules.NextBalance(0m, 5.0000m);
        var second = StockRules.NextBalance(first, 3.0000m);

        Assert.Equal(5.0000m, first);
        Assert.Equal(8.0000m, second);
        Assert.True(StockRules.DeferredTriggerAccepts([second], second));
        Assert.False(StockRules.DeferredTriggerAccepts([first, second], second));
        Assert.Equal("stock.reconciliation", ErrorCodes.StockReconciliation);
    }

    [Fact]
    public void Receipt_database_guards_use_stable_error_codes()
    {
        Assert.Equal("purchase.receipt_integrity", ErrorCodes.PurchaseReceiptIntegrity);
        Assert.Equal("purchase.receipt_immutable", ErrorCodes.PurchaseReceiptImmutable);
    }

    [Fact]
    public void Receipt_numbers_use_the_r_prefix_and_keep_gaps_usable()
    {
        Assert.Equal("R-000001", ReceiptNumbers.Format(1));
        Assert.Equal("R-000042", ReceiptNumbers.Format(42));
        Assert.Equal("R-1000000", ReceiptNumbers.Format(1_000_000));
    }
}
