using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("tax_rate", "pexilio", table =>
        {
            table.HasCheckConstraint("CK_tax_rate_range", "\"Rate\" >= 0 AND \"Rate\" <= 1");
        });
        builder.HasKey(rate => rate.Id);
        builder.Property(rate => rate.Name).HasMaxLength(40).IsRequired();
        builder.Property(rate => rate.Rate).HasColumnType("numeric(9,6)");
        builder.HasIndex(rate => new { rate.CompanyId, rate.Name }).IsUnique();
        builder.HasIndex(rate => rate.CompanyId)
            .IsUnique()
            .HasFilter("\"IsDefault\" = TRUE");
        builder.HasOne(rate => rate.Company)
            .WithMany(company => company.TaxRates)
            .HasForeignKey(rate => rate.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
