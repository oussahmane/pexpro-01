using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Sales;
using Pexilio.Infrastructure.Identity;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.ToTable("sale_return", "pexilio", table =>
        {
            table.HasCheckConstraint(
                "CK_sale_return_reason",
                "char_length(btrim(\"Reason\")) > 0 AND char_length(\"Reason\") <= 200");
            table.HasTrigger("sale_return_append_only");
            table.HasTrigger("sale_return_append_only_truncate");
        });
        builder.HasKey(saleReturn => saleReturn.Id);
        builder.HasAlternateKey(saleReturn => new { saleReturn.Id, saleReturn.SaleId, saleReturn.CompanyId }).HasName("AK_sale_return_identity");
        builder.Property(saleReturn => saleReturn.Number).HasMaxLength(32).IsRequired();
        builder.Property(saleReturn => saleReturn.Reason).HasMaxLength(200).IsRequired();
        builder.HasIndex(saleReturn => new { saleReturn.CompanyId, saleReturn.Number }).IsUnique().HasDatabaseName("UX_sale_return_company_number");
        builder.HasOne<Sale>().WithMany().HasForeignKey(saleReturn => new { saleReturn.SaleId, saleReturn.CompanyId })
            .HasPrincipalKey(sale => new { sale.Id, sale.CompanyId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_sale_return_sale");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(saleReturn => saleReturn.ReturnedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
