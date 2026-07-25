using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>Append-only loyalty history. Corrections are new adjustment rows, never rewrites.</summary>
public sealed class LoyaltyPointTransaction : BaseEntity
{
    public Guid CustomerLoyaltyAccountId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? ReferredCustomerId { get; set; }
    public Guid? InvoiceId { get; set; }
    public LoyaltyPointTransactionType Type { get; set; }
    public int Points { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
