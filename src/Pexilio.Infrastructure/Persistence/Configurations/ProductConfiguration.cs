using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Catalog;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product", "pexilio", table =>
        {
            table.HasCheckConstraint("CK_product_unit_price", "\"UnitPrice\" >= 0");
            table.HasCheckConstraint(
                "CK_product_price_bounds",
                """
                ("WholesaleUnitPrice" IS NULL OR "WholesaleUnitPrice" >= 0)
                AND ("MinimumUnitPrice" IS NULL OR "MinimumUnitPrice" >= 0)
                AND ("MinimumUnitPrice" IS NULL OR "MinimumUnitPrice" <= "UnitPrice")
                AND ("MinimumUnitPrice" IS NULL OR "WholesaleUnitPrice" IS NULL OR "MinimumUnitPrice" <= "WholesaleUnitPrice")
                """);
        });
        builder.HasKey(product => product.Id);
        builder.HasAlternateKey(product => new { product.Id, product.CompanyId })
            .HasName("AK_product_id_company");
        builder.Property(product => product.Sku).HasMaxLength(CatalogValidator.SkuMaxLength).IsRequired();
        builder.Property(product => product.SkuKey).HasMaxLength(CatalogValidator.SkuMaxLength).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(CatalogValidator.NameMaxLength).IsRequired();
        builder.Property(product => product.Unit).HasMaxLength(CatalogValidator.UnitMaxLength).IsRequired();
        builder.Property(product => product.UnitPrice).HasColumnType("numeric(19,4)");
        builder.Property(product => product.WholesaleUnitPrice).HasColumnType("numeric(19,4)");
        builder.Property(product => product.MinimumUnitPrice).HasColumnType("numeric(19,4)");
        builder.HasIndex(product => new { product.CompanyId, product.SkuKey }).IsUnique();
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Company.TaxRate>()
            .WithMany()
            .HasForeignKey(product => product.TaxRateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Company.Company>()
            .WithMany()
            .HasForeignKey(product => product.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
