using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pexilio.Domain.Sales;
using Pexilio.Infrastructure.Identity;

namespace Pexilio.Infrastructure.Persistence.Configurations;

public sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("sale_payment", "pexilio", table =>
        {
            table.HasCheckConstraint("CK_sale_payment_amount", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_sale_payment_method", "\"Method\" IN ('cash', 'transfer', 'card', 'other')");
            table.HasTrigger("sale_payment_append_only");
            table.HasTrigger("sale_payment_append_only_truncate");
            table.HasTrigger("sale_outstanding_on_payment");
        });
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Method).HasMaxLength(32).IsRequired();
        builder.Property(payment => payment.Amount).HasColumnType("numeric(19,4)");
        builder.Property(payment => payment.ReceiptNumber).HasMaxLength(32);
        builder.Property(payment => payment.Reference).HasMaxLength(CustomerPaymentValidator.ReferenceMaxLength);
        builder.Property(payment => payment.Notes).HasMaxLength(CustomerPaymentValidator.NotesMaxLength);
        builder.HasIndex(payment => new { payment.CompanyId, payment.ReceiptNumber })
            .IsUnique()
            .HasFilter("\"ReceiptNumber\" IS NOT NULL")
            .HasDatabaseName("UX_sale_payment_company_receipt");
        builder.HasOne<Sale>().WithMany(sale => sale.Payments).HasForeignKey(payment => new { payment.SaleId, payment.CompanyId })
            .HasPrincipalKey(sale => new { sale.Id, sale.CompanyId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_sale_payment_sale");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(payment => payment.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
