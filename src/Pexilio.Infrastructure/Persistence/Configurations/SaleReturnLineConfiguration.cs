using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Sales;
using Pexilio.Domain.Stock;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class SaleReturnLineConfiguration : IEntityTypeConfiguration<SaleReturnLine>
{
    public void Configure(EntityTypeBuilder<SaleReturnLine> builder)
    {
        builder.ToTable("sale_return_line", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_sale_return_line_amounts",
                "\"Quantity\" > 0 AND \"Amount\" >= 0");
            table.HasTrigger("sale_return_line_append_only");
            table.HasTrigger("sale_return_line_append_only_truncate");
            table.HasTrigger("sale_return_line_matches_movement");
            table.HasTrigger("sale_return_line_within_sale");
            table.HasTrigger("sale_outstanding_on_return_line");
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Quantity).HasColumnType("numeric(19,4)");
        builder.Property(line => line.Amount).HasColumnType("numeric(19,4)");
        builder.HasIndex(line => new { line.ReturnId, line.SaleLineId }).IsUnique().HasDatabaseName("UX_sale_return_line_return_sale_line");
        builder.HasIndex(line => line.StockMovementId).IsUnique().HasFilter("\"StockMovementId\" IS NOT NULL").HasDatabaseName("UX_sale_return_line_movement");
        builder.HasOne<SaleReturn>().WithMany(saleReturn => saleReturn.Lines)
            .HasForeignKey(line => new { line.ReturnId, line.SaleId, line.CompanyId })
            .HasPrincipalKey(saleReturn => new { saleReturn.Id, saleReturn.SaleId, saleReturn.CompanyId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_sale_return_line_return");
        builder.HasOne<SaleLine>().WithMany()
            .HasForeignKey(line => new { line.SaleLineId, line.SaleId, line.ProductId })
            .HasPrincipalKey(line => new { line.Id, line.SaleId, line.ProductId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_sale_return_line_sale_line");
        builder.HasOne<StockMovement>().WithMany().HasForeignKey(line => line.StockMovementId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_sale_return_line_movement");
    }
}
