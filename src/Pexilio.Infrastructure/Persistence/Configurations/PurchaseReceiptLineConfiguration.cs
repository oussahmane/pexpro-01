using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Purchasing;
using Pexilio.Domain.Stock;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class PurchaseReceiptLineConfiguration : IEntityTypeConfiguration<PurchaseReceiptLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.ToTable("purchase_receipt_line", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_receipt_line_quantity",
                "\"Quantity\" > 0 AND \"UnitCost\" >= 0");
            table.HasTrigger("purchase_receipt_line_matches_movement");
            table.HasTrigger("purchase_receipt_line_append_only");
            table.HasTrigger("purchase_receipt_line_append_only_truncate");
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Quantity).HasColumnType("numeric(19,4)");
        builder.Property(line => line.UnitCost).HasColumnType("numeric(19,4)");
        builder.HasIndex(line => new { line.ReceiptId, line.PurchaseLineId })
            .IsUnique()
            .HasDatabaseName("UX_receipt_line_receipt_purchase_line");
        builder.HasIndex(line => line.StockMovementId)
            .IsUnique()
            .HasDatabaseName("UX_receipt_line_movement");
        builder.HasOne<PurchaseReceipt>()
            .WithMany(receipt => receipt.Lines)
            .HasForeignKey(line => new { line.ReceiptId, line.PurchaseId, line.CompanyId })
            .HasPrincipalKey(receipt => new { receipt.Id, receipt.PurchaseId, receipt.CompanyId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_receipt_line_receipt");
        builder.HasOne<PurchaseLine>()
            .WithMany()
            .HasForeignKey(line => new { line.PurchaseLineId, line.PurchaseId, line.ProductId })
            .HasPrincipalKey(line => new { line.Id, line.PurchaseId, line.ProductId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_receipt_line_purchase_line");
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => new { line.ProductId, line.CompanyId })
            .HasPrincipalKey(product => new { product.Id, product.CompanyId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_receipt_line_product");
        builder.HasOne<StockMovement>()
            .WithMany()
            .HasForeignKey(line => line.StockMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_receipt_line_movement");
    }
}
