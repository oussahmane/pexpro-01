using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Purchasing;
using Pexilio.Infrastructure.Identity;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("purchase_receipt", "pexilio", table =>
        {
            table.HasTrigger("purchase_receipt_append_only");
            table.HasTrigger("purchase_receipt_append_only_truncate");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.Id, receipt.PurchaseId, receipt.CompanyId })
            .HasName("AK_receipt_identity");
        builder.Property(receipt => receipt.Number).HasMaxLength(PurchasingValidator.NumberMaxLength).IsRequired();
        builder.Property(receipt => receipt.DocumentRef).HasMaxLength(PurchasingValidator.DocumentRefMaxLength).IsRequired();
        builder.Property(receipt => receipt.DocumentKey).HasMaxLength(PurchasingValidator.DocumentRefMaxLength).IsRequired();
        builder.HasIndex(receipt => new { receipt.CompanyId, receipt.Number })
            .IsUnique()
            .HasDatabaseName("UX_receipt_company_number");
        builder.HasIndex(receipt => new { receipt.PurchaseId, receipt.DocumentKey })
            .IsUnique()
            .HasDatabaseName("UX_receipt_purchase_document");
        builder.HasOne<Purchase>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.PurchaseId, receipt.CompanyId })
            .HasPrincipalKey(purchase => new { purchase.Id, purchase.CompanyId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_receipt_purchase");
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(receipt => receipt.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_receipt_user");
    }
}
