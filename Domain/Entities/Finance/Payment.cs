using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Payment : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid CustomerId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public PaymentMethod Method { get; set; }
    public Guid? BankAccountId { get; set; }
    public string SettlementAccountCode { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.Confirmed;
    public DateTime PaidAt { get; set; }
    public string? Notes { get; set; }
}
