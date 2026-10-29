using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Stock;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balance", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_stock_balance_minimum",
                "\"MinimumQuantity\" IS NULL OR \"MinimumQuantity\" >= 0");
            table.HasTrigger("stock_balance_prevent_negative");
        });
        builder.HasKey(balance => balance.Id);
        builder.Property(balance => balance.Quantity).HasColumnType("numeric(19,4)");
        builder.Property(balance => balance.MinimumQuantity).HasColumnType("numeric(19,4)");
        builder.HasIndex(balance => new { balance.StockLocationId, balance.ProductId })
            .IsUnique()
            .HasDatabaseName("UX_stock_balance_location_product");
        builder.HasOne<StockLocation>()
            .WithMany()
            .HasForeignKey(balance => balance.StockLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(balance => balance.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
