using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Company;
using Pexilio.Domain.Sales;
using Pexilio.Domain.Stock;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        builder.ToTable("sale_line", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_sale_line_amounts",
                """
                "LineNumber" > 0
                AND "Quantity" > 0
                AND "ListUnitPrice" >= 0
                AND "UnitPrice" >= 0
                AND "LineDiscount" >= 0
                AND "AllocatedSaleDiscount" >= 0
                AND "AllocatedSaleDiscount" <= "LineTotal"
                AND "LineTotal" >= 0
                AND "LineTotal" = ("Quantity" * "UnitPrice") - "LineDiscount"
                AND "TaxRate" >= 0
                """);
            // CK_sale_line_price accepts a percentage rate from 0 through 100. It does not recompute
            // LineDiscount from that rate. SalesService is authoritative for the amount. Posted
            // lines stay append-only, so this limitation is not closed with a trigger.
            table.HasCheckConstraint(
                "CK_sale_line_price",
                """
                "PriceKind" IN ('retail', 'wholesale')
                AND (
                    ("DiscountKind" = 'none' AND "LineDiscount" = 0 AND "DiscountRate" IS NULL)
                    OR ("DiscountKind" = 'amount' AND "DiscountRate" IS NULL)
                    OR ("DiscountKind" = 'percent' AND "DiscountRate" >= 0 AND "DiscountRate" <= 100)
                )
                """);
            table.HasTrigger("sale_line_append_only");
            table.HasTrigger("sale_line_append_only_truncate");
            table.HasTrigger("sale_line_matches_movement");
        });
        builder.HasKey(line => line.Id);
        builder.HasAlternateKey(line => new { line.Id, line.SaleId, line.ProductId }).HasName("AK_sale_line_identity");
        builder.Property(line => line.Quantity).HasColumnType("numeric(19,4)");
        builder.Property(line => line.ListUnitPrice).HasColumnType("numeric(19,4)");
        builder.Property(line => line.UnitPrice).HasColumnType("numeric(19,4)");
        builder.Property(line => line.PriceKind).HasMaxLength(16).IsRequired().HasDefaultValue(PriceKinds.Retail);
        builder.Property(line => line.LineDiscount).HasColumnType("numeric(19,4)");
        builder.Property(line => line.DiscountKind).HasMaxLength(16).IsRequired().HasDefaultValue(DiscountKinds.None);
        builder.Property(line => line.DiscountRate).HasColumnType("numeric(7,4)");
        builder.Property(line => line.BelowMinimumOverride).HasDefaultValue(false);
        builder.Property(line => line.AllocatedSaleDiscount).HasColumnType("numeric(19,4)");
        builder.Property(line => line.LineTotal).HasColumnType("numeric(19,4)");
        builder.Property(line => line.ProductSku).HasMaxLength(CatalogValidator.SkuMaxLength).IsRequired();
        builder.Property(line => line.ProductName).HasMaxLength(CatalogValidator.NameMaxLength).IsRequired();
        builder.Property(line => line.ProductUnit).HasMaxLength(CatalogValidator.UnitMaxLength).IsRequired();
        builder.Property(line => line.TaxRate).HasColumnType("numeric(9,6)");
        builder.HasIndex(line => new { line.SaleId, line.LineNumber }).IsUnique();
        builder.HasIndex(line => new { line.SaleId, line.ProductId }).IsUnique().HasDatabaseName("UX_sale_line_sale_product");
        builder.HasIndex(line => line.StockMovementId).IsUnique().HasFilter("\"StockMovementId\" IS NOT NULL").HasDatabaseName("UX_sale_line_movement");
        builder.HasOne<Sale>().WithMany(sale => sale.Lines).HasForeignKey(line => new { line.SaleId, line.CompanyId })
            .HasPrincipalKey(sale => new { sale.Id, sale.CompanyId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_sale_line_sale");
        builder.HasOne<Product>().WithMany().HasForeignKey(line => new { line.ProductId, line.CompanyId })
            .HasPrincipalKey(product => new { product.Id, product.CompanyId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_sale_line_product");
        builder.HasOne<TaxRate>().WithMany().HasForeignKey(line => line.TaxRateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockMovement>().WithMany().HasForeignKey(line => line.StockMovementId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_sale_line_movement");
    }
}
