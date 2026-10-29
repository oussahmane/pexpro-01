using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Company;
using Pexilio.Domain.Stock;
using Pexilio.Infrastructure.Identity;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movement", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_stock_movement_delta",
                """
                "QuantityDelta" <> 0
                AND (
                    ("MovementType" IN ('opening', 'receipt', 'sale_return') AND "QuantityDelta" > 0)
                    OR ("MovementType" = 'adjustment' AND "QuantityDelta" <> 0)
                    OR ("MovementType" = 'sale' AND "QuantityDelta" < 0)
                )
                """);
            table.HasCheckConstraint(
                "CK_stock_movement_reason",
                "char_length(btrim(\"Reason\")) > 0 AND char_length(\"Reason\") <= 200");
            table.HasCheckConstraint(
                "CK_stock_movement_unit_cost",
                "\"UnitCost\" IS NULL OR \"UnitCost\" >= 0");
            table.HasCheckConstraint(
                "CK_stock_movement_source",
                "\"SourceType\" IS NULL OR \"SourceType\" IN ('manual', 'purchase', 'sale', 'sale_return')");
            table.HasTrigger("stock_movement_append_only");
            table.HasTrigger("stock_movement_append_only_truncate");
            table.HasTrigger("stock_movement_matches_balance");
        });
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.MovementType).HasMaxLength(32).IsRequired();
        builder.Property(movement => movement.Reason).HasMaxLength(StockRules.ReasonMaxLength).IsRequired();
        builder.Property(movement => movement.QuantityDelta).HasColumnType("numeric(19,4)");
        builder.Property(movement => movement.BalanceAfter).HasColumnType("numeric(19,4)");
        builder.Property(movement => movement.UnitCost).HasColumnType("numeric(19,4)");
        builder.Property(movement => movement.SourceType).HasMaxLength(32);
        builder.HasIndex(movement => new { movement.CompanyId, movement.ProductId, movement.OccurredAtUtc });
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(movement => movement.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>()
            .WithMany()
            .HasForeignKey(movement => movement.StockLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(movement => movement.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
