namespace Pexilio.Domain.Sales;

public sealed class SalePayment
{
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public Guid CompanyId { get; set; }

    public string Method { get; set; } = SaleRules.CashMethod;

    public decimal Amount { get; set; }

    public string? ReceiptNumber { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset PaidAtUtc { get; set; }

    public Guid ReceivedByUserId { get; set; }
}
