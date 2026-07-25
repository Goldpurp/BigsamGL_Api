using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class PurchasePayout : BaseEntity
{
    public string PayoutNumber { get; set; } = string.Empty;
    public Guid CompanyPurchaseId { get; set; }
    public Guid FinancialTransactionId { get; set; }
    public Guid TransactionDocumentId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public PaymentMethod Method { get; set; }
    public Guid? BankAccountId { get; set; }
    public string SettlementAccountCode { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
    public PurchasePayoutStatus Status { get; set; } = PurchasePayoutStatus.Confirmed;
    public Guid PaidByUserId { get; set; }
    public string? Notes { get; set; }
}

