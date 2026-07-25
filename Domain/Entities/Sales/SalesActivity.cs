using Domain.Common;

namespace Domain.Entities;

public class SalesActivity : BaseEntity
{
    public Guid SalesLeadId { get; set; }
    public Guid StaffUserId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? FollowUpAt { get; set; }
}
