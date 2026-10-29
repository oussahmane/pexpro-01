using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;
using Pexilio.Domain.Purchasing;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class PurchaseIdempotencyConfiguration : IEntityTypeConfiguration<PurchaseIdempotency>
{
    public void Configure(EntityTypeBuilder<PurchaseIdempotency> builder)
    {
        builder.ToTable("purchase_idempotency", "pexilio");
        builder.HasKey(claim => claim.Id);
        builder.Property(claim => claim.Operation).HasMaxLength(32).IsRequired();
        builder.Property(claim => claim.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(claim => claim.ResponseJson).HasColumnType("text").IsRequired();
        builder.HasIndex(claim => new { claim.CompanyId, claim.Operation, claim.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_purchase_idempotency_company_operation_key");
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(claim => claim.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
