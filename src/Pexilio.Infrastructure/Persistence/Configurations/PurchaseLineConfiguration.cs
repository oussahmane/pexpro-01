using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Purchasing;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class PurchaseLineConfiguration : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> builder)
    {
        builder.ToTable("purchase_line", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_purchase_line_amounts",
                """
                "QuantityOrdered" > 0
                AND "QuantityReceived" >= 0
                AND "QuantityReceived" <= "QuantityOrdered"
                AND "UnitCost" >= 0
                """);
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.QuantityOrdered).HasColumnType("numeric(19,4)");
        builder.Property(line => line.QuantityReceived).HasColumnType("numeric(19,4)").HasDefaultValue(0m);
        builder.Property(line => line.UnitCost).HasColumnType("numeric(19,4)");
        builder.Property(line => line.ProductSku).HasMaxLength(CatalogValidator.SkuMaxLength).IsRequired();
        builder.Property(line => line.ProductName).HasMaxLength(CatalogValidator.NameMaxLength).IsRequired();
        builder.Property(line => line.ProductUnit).HasMaxLength(CatalogValidator.UnitMaxLength).IsRequired();
        builder.HasIndex(line => new { line.PurchaseId, line.LineNumber }).IsUnique();
        builder.HasIndex(line => new { line.PurchaseId, line.ProductId }).IsUnique();
        builder.HasOne<Purchase>()
            .WithMany(purchase => purchase.Lines)
            .HasForeignKey(line => line.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
