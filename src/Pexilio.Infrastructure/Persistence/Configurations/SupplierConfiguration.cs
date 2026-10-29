using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;
using Pexilio.Domain.Purchasing;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("supplier", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_supplier_name",
                "char_length(btrim(\"Name\")) > 0 AND char_length(\"Name\") <= 160");
        });
        builder.HasKey(supplier => supplier.Id);
        builder.Property(supplier => supplier.Name).HasMaxLength(PurchasingValidator.SupplierNameMaxLength).IsRequired();
        builder.Property(supplier => supplier.NameKey).HasMaxLength(PurchasingValidator.SupplierNameMaxLength).IsRequired();
        builder.Property(supplier => supplier.ContactName).HasMaxLength(PurchasingValidator.ContactNameMaxLength);
        builder.Property(supplier => supplier.Phone).HasMaxLength(PurchasingValidator.PhoneMaxLength);
        builder.Property(supplier => supplier.Email).HasMaxLength(PurchasingValidator.EmailMaxLength);
        builder.Property(supplier => supplier.Address).HasMaxLength(PurchasingValidator.AddressMaxLength);
        builder.Property(supplier => supplier.TaxId).HasMaxLength(PurchasingValidator.TaxIdMaxLength);
        builder.Property(supplier => supplier.Notes).HasMaxLength(PurchasingValidator.NotesMaxLength);
        builder.Property(supplier => supplier.IsActive).HasDefaultValue(true);
        builder.HasIndex(supplier => new { supplier.CompanyId, supplier.NameKey })
            .IsUnique()
            .HasDatabaseName("UX_supplier_company_name");
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(supplier => supplier.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
