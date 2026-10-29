using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Catalog;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("category", "pexilio");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).HasMaxLength(CatalogValidator.CategoryNameMaxLength).IsRequired();
        builder.Property(category => category.NameKey).HasMaxLength(CatalogValidator.CategoryNameMaxLength).IsRequired();
        builder.HasIndex(category => new { category.CompanyId, category.NameKey })
            .IsUnique()
            .HasFilter("\"ParentId\" IS NULL");
        builder.HasIndex(category => new { category.CompanyId, category.ParentId, category.NameKey })
            .IsUnique()
            .HasFilter("\"ParentId\" IS NOT NULL");
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(category => category.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Company.Company>()
            .WithMany()
            .HasForeignKey(category => category.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
