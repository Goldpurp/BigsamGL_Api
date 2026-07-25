using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class ContactInteraction : BaseEntity
{
    public Guid ContactId { get; set; }
    public ContactInteractionType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? NextActionAt { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid? SalesLeadId { get; set; }
}
