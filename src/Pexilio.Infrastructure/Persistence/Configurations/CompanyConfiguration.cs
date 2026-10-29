using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Company;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("company", "pexilio", table =>
        {
            table.HasCheckConstraint("CK_company_singleton", "\"SingletonKey\" = 1");
            table.HasCheckConstraint("CK_company_currency_scale", "\"CurrencyScale\" >= 0 AND \"CurrencyScale\" <= 4");
        });
        builder.HasKey(company => company.Id);
        builder.Property(company => company.SingletonKey).HasDefaultValue(1);
        builder.HasIndex(company => company.SingletonKey).IsUnique();
        builder.Property(company => company.LegalName).HasMaxLength(CompanyProfileValidator.NameMaxLength).IsRequired();
        builder.Property(company => company.TradeName).HasMaxLength(CompanyProfileValidator.NameMaxLength);
        builder.Property(company => company.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(company => company.TimeZoneId).HasMaxLength(CompanyProfileValidator.TimeZoneMaxLength).IsRequired();
        builder.Property(company => company.TaxMode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(company => company.Rc).HasMaxLength(CompanyProfileValidator.IdentifierMaxLength);
        builder.Property(company => company.Nif).HasMaxLength(CompanyProfileValidator.IdentifierMaxLength);
        builder.Property(company => company.Nis).HasMaxLength(CompanyProfileValidator.IdentifierMaxLength);
        builder.Property(company => company.Ai).HasMaxLength(CompanyProfileValidator.IdentifierMaxLength);
    }
}
