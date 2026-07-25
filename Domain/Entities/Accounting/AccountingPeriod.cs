using Domain.Common;

namespace Domain.Entities;

public sealed class AccountingPeriod : BaseEntity
{
    public Guid BranchId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public string? Reason { get; set; }
}
