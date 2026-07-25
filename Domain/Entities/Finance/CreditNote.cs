using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>Customer credit liability. It never represents a cash refund.</summary>
public sealed class CreditNote : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? OriginalInvoiceId { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal OriginalAmount { get; set; }
    public decimal UsedAmount { get; set; }
    public decimal AvailableAmount => Math.Max(0, OriginalAmount - UsedAmount);
    public CreditNoteState State { get; set; } = CreditNoteState.CreditAvailable;
    public string Reason { get; set; } = string.Empty;
    public Guid ApprovedByUserId { get; set; }
    public DateTime IssuedAt { get; set; }
    public long RowVersion { get; set; } = 1;
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}

/// <summary>Immutable application of customer credit to a compatible sales invoice.</summary>
public sealed class CreditApplication : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid CreditNoteId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime AppliedAt { get; set; }
    public Guid AppliedByUserId { get; set; }
    public Guid IdempotencyKey { get; set; }
}
