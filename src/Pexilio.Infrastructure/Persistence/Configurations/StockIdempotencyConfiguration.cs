using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;
using Pexilio.Domain.Stock;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class StockIdempotencyConfiguration : IEntityTypeConfiguration<StockIdempotency>
{
    public void Configure(EntityTypeBuilder<StockIdempotency> builder)
    {
        builder.ToTable("stock_idempotency", "pexilio");
        builder.HasKey(claim => claim.Id);
        builder.Property(claim => claim.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(claim => claim.ResponseJson).HasColumnType("text").IsRequired();
        builder.HasIndex(claim => new { claim.CompanyId, claim.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_stock_idempotency_company_key");
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(claim => claim.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
