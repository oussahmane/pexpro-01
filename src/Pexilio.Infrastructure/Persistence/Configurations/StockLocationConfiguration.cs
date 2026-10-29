using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;
using Pexilio.Domain.Stock;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    public void Configure(EntityTypeBuilder<StockLocation> builder)
    {
        builder.ToTable("stock_location", "pexilio");
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Code).HasMaxLength(16).IsRequired();
        builder.Property(location => location.Name).HasMaxLength(120).IsRequired();
        builder.Property(location => location.PreventNegativeStock).HasDefaultValue(true);
        builder.HasIndex(location => new { location.CompanyId, location.Code }).IsUnique();
        builder.HasIndex(location => location.CompanyId)
            .IsUnique()
            .HasFilter("\"IsDefault\"")
            .HasDatabaseName("UX_stock_location_default");
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(location => location.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
