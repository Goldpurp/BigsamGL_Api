using Domain.Common;

namespace Domain.Entities;

public sealed class StaffBranchMembership : BaseEntity
{
    public Guid UserAccountId { get; set; }
    public Guid BranchId { get; set; }
    public bool IsPrimary { get; set; }
}
