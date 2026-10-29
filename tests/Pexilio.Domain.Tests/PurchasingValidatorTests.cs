using Pexilio.Domain.Purchasing;

namespace Pexilio.Domain.Tests;

public sealed class PurchasingValidatorTests
{
    [Fact]
    public void Supplier_name_is_required_and_matched_without_regard_to_case_or_outer_space()
    {
        var saved = PurchasingValidator.TrySupplier("  Atlas  ", null, null, null, null, null, null, null, out var draft, out var errors);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal("Atlas", draft.Name);
        Assert.Equal("ATLAS", draft.NameKey);
        Assert.True(draft.IsActive);
    }

    [Theory]
    [InlineData("", "name.required")]
    [InlineData("   ", "name.required")]
    public void Supplier_rejects_a_blank_name(string name, string code)
    {
        var saved = PurchasingValidator.TrySupplier(name, null, null, null, null, null, null, true, out _, out var errors);

        Assert.False(saved);
        Assert.Contains(errors, error => error.Field == "name" && error.Code == code);
    }

    [Fact]
    public void Supplier_name_rejects_a_match_key_that_expands_past_the_column()
    {
        const string ligature = "\uFB03";
        Assert.True(PurchasingValidator.Key(ligature).Length > ligature.Length);
        var expanded = new string(ligature[0], PurchasingValidator.SupplierNameMaxLength);
        var rejected = PurchasingValidator.TrySupplier(expanded, null, null, null, null, null, null, true, out _, out var errors);
        var accepted = PurchasingValidator.TrySupplier(ligature, null, null, null, null, null, null, true, out var draft, out var acceptedErrors);
        var sameKey = PurchasingValidator.TrySupplier("FFI", null, null, null, null, null, null, true, out var upper, out _);

        Assert.False(rejected);
        Assert.Contains(errors, error => error.Field == "name" && error.Code == "name.length");
        Assert.True(accepted);
        Assert.Empty(acceptedErrors);
        Assert.Equal(ligature, draft.Name);
        Assert.Equal("FFI", draft.NameKey);
        Assert.True(sameKey);
        Assert.Equal(draft.NameKey, upper.NameKey);
    }

    [Fact]
    public void Supplier_rejects_an_email_without_a_domain()
    {
        var saved = PurchasingValidator.TrySupplier("Atlas", null, null, "owner@localhost", null, null, null, true, out _, out var errors);

        Assert.False(saved);
        Assert.Contains(errors, error => error.Code == "email.invalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Document_reference_blank_is_stored_as_absent(string? reference)
    {
        var saved = PurchasingValidator.TryDraft(reference, null, [], out var draft, out var errors);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Null(draft.SupplierDocumentRef);
        Assert.Null(draft.SupplierDocumentKey);
    }

    [Fact]
    public void Document_reference_keeps_the_supplier_text_and_a_separate_match_key()
    {
        var saved = PurchasingValidator.TryDraft("  FA 2026/014  ", null, [], out var draft, out _);

        Assert.True(saved);
        Assert.Equal("FA 2026/014", draft.SupplierDocumentRef);
        Assert.Equal("FA 2026/014", draft.SupplierDocumentKey);
        var lower = PurchasingValidator.TryDraft("bl-1", null, [], out var lowerDraft, out _);
        Assert.True(lower);
        Assert.Equal("bl-1", lowerDraft.SupplierDocumentRef);
        Assert.Equal("BL-1", lowerDraft.SupplierDocumentKey);
        Assert.Equal("A-000001", PurchaseNumbers.Format(1));
        Assert.Equal("A-1000000", PurchaseNumbers.Format(1_000_000));
    }

    [Fact]
    public void Document_reference_rejects_characters_outside_the_allowed_set()
    {
        var saved = PurchasingValidator.TryDraft("FA@014", null, [], out _, out var errors);

        Assert.False(saved);
        Assert.Contains(errors, error => error.Code == "supplierDocumentRef.invalid");
    }

    [Fact]
    public void Document_reference_rejects_a_match_key_that_expands_past_the_column()
    {
        const string ligature = "\uFB03";
        Assert.True(PurchasingValidator.Key(ligature).Length > ligature.Length);
        var expanded = new string(ligature[0], PurchasingValidator.DocumentRefMaxLength);
        var rejected = PurchasingValidator.TryDraft(expanded, null, [], out _, out var errors);
        var accepted = PurchasingValidator.TryDraft(ligature, null, [], out var draft, out var acceptedErrors);
        var sameKey = PurchasingValidator.TryDraft("ffi", null, [], out var lower, out _);

        Assert.False(rejected);
        Assert.Contains(errors, error => error.Field == "supplierDocumentRef" && error.Code == "supplierDocumentRef.length");
        Assert.True(accepted);
        Assert.Empty(acceptedErrors);
        Assert.Equal(ligature, draft.SupplierDocumentRef);
        Assert.Equal("FFI", draft.SupplierDocumentKey);
        Assert.True(sameKey);
        Assert.Equal(draft.SupplierDocumentKey, lower.SupplierDocumentKey);
    }

    [Fact]
    public void Draft_line_keeps_exact_quantity_and_unit_cost_and_rejects_a_fifth_decimal()
    {
        var productId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var saved = PurchasingValidator.TryDraft(
            null,
            null,
            [(productId, "1.2500", "19.5000")],
            out var draft,
            out var errors);
        var tooFine = PurchasingValidator.TryDraft(
            null,
            null,
            [(productId, "1.0000", "1.23456")],
            out _,
            out var scaleErrors);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal(1.2500m, draft.Lines[0].Quantity);
        Assert.Equal(19.5000m, draft.Lines[0].UnitCost);
        Assert.False(tooFine);
        Assert.Contains(scaleErrors, error => error.Code == "unitCost.scale");
    }

    [Fact]
    public void Draft_line_rejects_zero_quantity_a_negative_cost_and_a_repeated_product()
    {
        var productId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var zero = PurchasingValidator.TryDraft(null, null, [(productId, "0.0000", "1.0000")], out _, out var zeroErrors);
        var negative = PurchasingValidator.TryDraft(null, null, [(productId, "1.0000", "-1.0000")], out _, out var negativeErrors);
        var duplicate = PurchasingValidator.TryDraft(
            null,
            null,
            [(productId, "1.0000", "1.0000"), (productId, "2.0000", "1.0000")],
            out _,
            out var duplicateErrors);

        Assert.False(zero);
        Assert.Contains(zeroErrors, error => error.Code == "quantity.positive");
        Assert.False(negative);
        Assert.Contains(negativeErrors, error => error.Code == "unitCost.sign");
        Assert.False(duplicate);
        Assert.Contains(duplicateErrors, error => error.Code == "lines.duplicate");
    }

    [Fact]
    public void An_empty_draft_has_no_lines_and_no_selling_price_field()
    {
        var saved = PurchasingValidator.TryDraft(null, "  note  ", null, out var draft, out _);

        Assert.True(saved);
        Assert.Empty(draft.Lines);
        Assert.Equal("note", draft.Notes);
    }

    [Fact]
    public void Draft_line_allows_a_zero_unit_cost()
    {
        var saved = PurchasingValidator.TryDraft(
            null,
            null,
            [(Guid.NewGuid(), "1.0000", "0.0000")],
            out var draft,
            out var errors);

        Assert.True(saved);
        Assert.Empty(errors);
        Assert.Equal(0m, draft.Lines[0].UnitCost);
    }

    [Theory]
    [InlineData(PurchaseStatuses.Draft, true)]
    [InlineData(PurchaseStatuses.Ordered, false)]
    [InlineData(PurchaseStatuses.PartiallyReceived, false)]
    [InlineData(PurchaseStatuses.Received, false)]
    [InlineData(PurchaseStatuses.Cancelled, false)]
    public void Only_a_draft_can_be_created_or_changed(string status, bool writable)
    {
        Assert.Equal(writable, PurchaseStatuses.CanMutate(status));
    }

    [Fact]
    public void Replaying_a_deleted_purchase_does_not_return_the_stored_purchase()
    {
        Assert.Equal(PurchaseReplayOutcome.ReturnStored, PurchaseReplay.Decide(true, true, true));
        Assert.Equal(PurchaseReplayOutcome.Missing, PurchaseReplay.Decide(true, true, false));
        Assert.Equal(PurchaseReplayOutcome.Conflict, PurchaseReplay.Decide(false, true, false));
        Assert.Equal(PurchaseReplayOutcome.Conflict, PurchaseReplay.Decide(true, false, false));
    }
}
