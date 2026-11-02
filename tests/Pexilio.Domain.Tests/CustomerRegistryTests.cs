using Pexilio.Domain;
using Pexilio.Domain.Customers;
using Pexilio.Domain.Sales;

namespace Pexilio.Domain.Tests;

public sealed class CustomerRegistryTests
{
    [Fact]
    public void Two_customers_may_share_a_name_and_the_same_normalized_key()
    {
        var first = CustomerValidator.TryCustomer(
            "individual", "Amin", "0551000001", null, null, null, null, null, null, null, null, out var firstDraft, out var firstErrors);
        var second = CustomerValidator.TryCustomer(
            "individual", " amin ", "0551000002", null, null, null, null, null, null, null, true, out var secondDraft, out var secondErrors);

        Assert.True(first);
        Assert.True(second);
        Assert.Empty(firstErrors);
        Assert.Empty(secondErrors);
        Assert.Equal("Amin", firstDraft.Name);
        Assert.Equal("amin", secondDraft.Name);
        Assert.Equal(firstDraft.NameKey, secondDraft.NameKey);
        Assert.Equal("AMIN", firstDraft.NameKey);
        Assert.NotEqual(firstDraft.Phone, secondDraft.Phone);
    }

    [Fact]
    public void An_individual_cannot_store_business_identifiers_and_a_business_can()
    {
        var individual = CustomerValidator.TryCustomer(
            "individual", "Amin", null, null, null, null, "123", null, null, null, null, out _, out var individualErrors);
        var business = CustomerValidator.TryCustomer(
            "business", "Atlas", null, "Rue 1", null, "12 B 34", "0001", null, null, "100.0000", true, out var draft, out var businessErrors);

        Assert.False(individual);
        Assert.Contains(new FieldError("type", "customer.identifier"), individualErrors);
        Assert.True(business);
        Assert.Empty(businessErrors);
        Assert.Equal(CustomerTypes.Business, draft.Type);
        Assert.Equal("12 B 34", draft.Rc);
        Assert.Equal("0001", draft.Nif);
        Assert.Equal(100.0000m, draft.CreditLimit);
    }

    [Fact]
    public void A_cashier_can_create_and_edit_contact_fields_but_not_restricted_fields()
    {
        Assert.True(CustomerValidator.TryCustomer(
            "business", "Atlas", "0551", null, null, null, null, null, null, null, null, out var created, out _));
        Assert.True(CustomerAccess.TryCreate(false, created, out var cashierCreate, out var createErrors));
        Assert.Empty(createErrors);
        Assert.True(cashierCreate.IsActive);
        Assert.Null(cashierCreate.Nif);

        Assert.True(CustomerValidator.TryCustomer(
            "business", "Atlas", "0551", null, null, null, "0001", null, null, null, null, out var withNif, out _));
        Assert.False(CustomerAccess.TryCreate(false, withNif, out _, out var nifErrors));
        Assert.Contains(new FieldError("nif", "customer.forbidden"), nifErrors);

        var stored = new CustomerStored(CustomerTypes.Business, "12 B 34", "0001", null, null, 50.0000m, true);
        Assert.True(CustomerValidator.TryCustomer(
            "business", "Atlas Eau", "0552", "Rue 2", "note", null, null, null, null, null, null, out var contact, out _));
        Assert.True(CustomerAccess.TryUpdate(false, contact, stored, out var updated, out var updateErrors));
        Assert.Empty(updateErrors);
        Assert.Equal("Atlas Eau", updated.Name);
        Assert.Equal("0552", updated.Phone);
        Assert.Equal("Rue 2", updated.Address);
        Assert.Equal("note", updated.Notes);
        Assert.Equal("0001", updated.Nif);
        Assert.Equal(50.0000m, updated.CreditLimit);
        Assert.True(updated.IsActive);

        Assert.True(CustomerValidator.TryCustomer(
            "individual", "Atlas Eau", "0552", null, null, null, null, null, null, null, false, out var restricted, out _));
        Assert.False(CustomerAccess.TryUpdate(false, restricted, stored, out _, out var restrictedErrors));
        Assert.Contains(new FieldError("type", "customer.forbidden"), restrictedErrors);
        Assert.Contains(new FieldError("isActive", "customer.forbidden"), restrictedErrors);
    }

    [Fact]
    public void An_administrator_can_set_identifiers_the_limit_and_inactive_status()
    {
        Assert.True(CustomerValidator.TryCustomer(
            "business", "Atlas", null, null, null, null, "0001", null, null, "25.5000", false, out var requested, out _));
        Assert.True(CustomerAccess.TryCreate(true, requested, out var created, out var errors));
        Assert.Empty(errors);
        Assert.Equal("0001", created.Nif);
        Assert.Equal(25.5000m, created.CreditLimit);
        Assert.False(created.IsActive);
    }

    [Fact]
    public void A_sale_accepts_one_cash_tender_and_rejects_a_second()
    {
        var product = Guid.NewGuid();
        var split = SaleValidator.TryCashSale(
            [(product, "1.0000", "10.0000", "10.0000")],
            "10.0000",
            [("cash", "6.0000"), ("cash", "4.0000")],
            out _,
            out var errors);

        Assert.False(split);
        Assert.Contains(new FieldError("payments", "payments.single"), errors);
    }
}
