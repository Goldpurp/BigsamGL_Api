using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class TransactionDocument : BaseEntity
{
    public string DocumentNumber { get; set; } = string.Empty;
    public FinancialDocumentType Type { get; set; }
    public FinancialDocumentStatus Status { get; set; } = FinancialDocumentStatus.Issued;
    public Guid FinancialTransactionId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? CompanyPurchaseId { get; set; }
    public Guid? PurchasePayoutId { get; set; }
    public DateTime IssuedAt { get; set; }
    public Guid IssuedByUserId { get; set; }
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}

