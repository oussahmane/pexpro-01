using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;
using Pexilio.Domain.Purchasing;
using Pexilio.Infrastructure.Identity;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchase", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_purchase_status",
                "\"Status\" IN ('draft', 'ordered', 'partially_received', 'received', 'cancelled')");
            table.HasCheckConstraint(
                "CK_purchase_document",
                """
                ("SupplierDocumentRef" IS NULL AND "SupplierDocumentKey" IS NULL)
                OR ("SupplierDocumentRef" IS NOT NULL AND "SupplierDocumentKey" IS NOT NULL)
                """);
        });
        builder.HasKey(purchase => purchase.Id);
        builder.Property(purchase => purchase.Number).HasMaxLength(PurchasingValidator.NumberMaxLength).IsRequired();
        builder.Property(purchase => purchase.Status).HasMaxLength(32).IsRequired();
        builder.Property(purchase => purchase.SupplierName).HasMaxLength(PurchasingValidator.SupplierNameMaxLength).IsRequired();
        builder.Property(purchase => purchase.SupplierTaxId).HasMaxLength(PurchasingValidator.TaxIdMaxLength);
        builder.Property(purchase => purchase.SupplierDocumentRef).HasMaxLength(PurchasingValidator.DocumentRefMaxLength);
        builder.Property(purchase => purchase.SupplierDocumentKey).HasMaxLength(PurchasingValidator.DocumentRefMaxLength);
        builder.Property(purchase => purchase.Notes).HasMaxLength(PurchasingValidator.NotesMaxLength);
        builder.Property(purchase => purchase.Version).IsRowVersion();
        builder.HasIndex(purchase => new { purchase.CompanyId, purchase.Number })
            .IsUnique()
            .HasDatabaseName("UX_purchase_company_number");
        builder.HasIndex(purchase => new { purchase.CompanyId, purchase.SupplierId, purchase.SupplierDocumentKey })
            .IsUnique()
            .HasFilter("\"SupplierDocumentKey\" IS NOT NULL AND \"Status\" <> 'cancelled'")
            .HasDatabaseName("UX_purchase_supplier_document");
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(purchase => purchase.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(purchase => purchase.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(purchase => purchase.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
