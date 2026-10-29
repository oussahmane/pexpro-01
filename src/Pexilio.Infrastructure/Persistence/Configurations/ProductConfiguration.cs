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
        });
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Sku).HasMaxLength(CatalogValidator.SkuMaxLength).IsRequired();
        builder.Property(product => product.SkuKey).HasMaxLength(CatalogValidator.SkuMaxLength).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(CatalogValidator.NameMaxLength).IsRequired();
        builder.Property(product => product.Unit).HasMaxLength(CatalogValidator.UnitMaxLength).IsRequired();
        builder.Property(product => product.UnitPrice).HasColumnType("numeric(19,4)");
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
