using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;
using Pexilio.Domain.Customers;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_customer_name",
                "char_length(btrim(\"Name\")) > 0 AND char_length(\"Name\") <= 160 AND char_length(\"NameKey\") > 0");
            table.HasCheckConstraint(
                "CK_customer_type",
                "\"Type\" IN ('individual', 'business')");
            table.HasCheckConstraint(
                "CK_customer_identifiers",
                """
                "Type" = 'business'
                OR ("Rc" IS NULL AND "Nif" IS NULL AND "Nis" IS NULL AND "Ai" IS NULL)
                """);
            table.HasCheckConstraint(
                "CK_customer_credit_limit",
                "\"CreditLimit\" IS NULL OR \"CreditLimit\" >= 0");
        });
        builder.HasKey(customer => customer.Id);
        builder.HasAlternateKey(customer => new { customer.Id, customer.CompanyId }).HasName("AK_customer_id_company");
        builder.Property(customer => customer.Type).HasMaxLength(16).IsRequired();
        builder.Property(customer => customer.Name).HasMaxLength(CustomerValidator.NameMaxLength).IsRequired();
        builder.Property(customer => customer.NameKey).HasMaxLength(CustomerValidator.NameMaxLength).IsRequired();
        builder.Property(customer => customer.Phone).HasMaxLength(CustomerValidator.PhoneMaxLength);
        builder.Property(customer => customer.Address).HasMaxLength(CustomerValidator.AddressMaxLength);
        builder.Property(customer => customer.Notes).HasMaxLength(CustomerValidator.NotesMaxLength);
        builder.Property(customer => customer.Rc).HasMaxLength(CompanyProfileValidator.IdentifierMaxLength);
        builder.Property(customer => customer.Nif).HasMaxLength(CompanyProfileValidator.IdentifierMaxLength);
        builder.Property(customer => customer.Nis).HasMaxLength(CompanyProfileValidator.IdentifierMaxLength);
        builder.Property(customer => customer.Ai).HasMaxLength(CompanyProfileValidator.IdentifierMaxLength);
        builder.Property(customer => customer.CreditLimit).HasColumnType("numeric(19,4)");
        builder.Property(customer => customer.IsActive).HasDefaultValue(true);
        builder.HasIndex(customer => new { customer.CompanyId, customer.NameKey })
            .IsUnique(false)
            .HasDatabaseName("IX_customer_company_name_key");
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(customer => customer.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
