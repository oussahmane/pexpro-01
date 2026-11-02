using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;
using Pexilio.Domain.Sales;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class DocumentCounterConfiguration : IEntityTypeConfiguration<DocumentCounter>
{
    public void Configure(EntityTypeBuilder<DocumentCounter> builder)
    {
        builder.ToTable("document_counter", "pexilio", table =>
        {
            table.HasCheckConstraint("CK_document_counter_value", "\"LastValue\" > 0");
        });
        builder.HasKey(counter => new { counter.CompanyId, counter.Series });
        builder.Property(counter => counter.Series).HasMaxLength(32).IsRequired();
        builder.HasOne<Company>().WithMany().HasForeignKey(counter => counter.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
