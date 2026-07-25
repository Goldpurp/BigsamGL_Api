using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class FinancialTransaction : BaseEntity
{
    public Guid BranchId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public FinancialTransactionDirection Direction { get; set; }
    public FinancialTransactionType Type { get; set; }
    public FinancialTransactionStatus Status { get; set; } = FinancialTransactionStatus.Posted;
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public Guid? CounterpartyId { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public PaymentMethod Method { get; set; }
    public string SettlementAccountCode { get; set; } = string.Empty;
    public Guid? BankAccountId { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public Guid RecordedByUserId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public FreightClassification FreightClassification { get; set; } = FreightClassification.Other;
    public DateTime FreightClassifiedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReversalOfTransactionId { get; set; }
}

