using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class CommunicationMessage : BaseEntity
{
    public Guid CustomerId { get; set; }
    public CommunicationType Type { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public CommunicationStatus Status { get; set; } = CommunicationStatus.Queued;
    public string Provider { get; set; } = "BiGSAM Communications";
    public string ProviderReference { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
}
