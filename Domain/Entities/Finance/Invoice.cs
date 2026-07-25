using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Invoice : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceType Type { get; set; } = InvoiceType.Sales;
    public Guid? OriginalInvoiceId { get; set; }
    public string IssuerBranchCode { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public InvoicePaymentStatus PaymentStatus { get; set; } = InvoicePaymentStatus.Unpaid;
    public PaymentMethod? PreferredPaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string Currency { get; set; } = "GBP";
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AppliedCreditAmount { get; set; }
    public string? Notes { get; set; }
    public long RowVersion { get; set; } = 1;
    public string VerificationCode { get; set; } = string.Empty;
    public decimal BalanceDue => Math.Max(0, TotalAmount - AmountPaid - AppliedCreditAmount);
}
