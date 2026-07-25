using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class EmailBroadcast : BaseEntity
{
    public string CampaignNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ReplyTo { get; set; }
    public EmailBroadcastPurpose Purpose { get; set; }
    public EmailBroadcastStatus Status { get; set; } = EmailBroadcastStatus.Draft;
    public string AudienceSelectionJson { get; set; } = "{}";
    public Guid CreatedByUserId { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public int TotalRecipients { get; set; }
    public int PendingRecipients { get; set; }
    public int SentRecipients { get; set; }
    public int FailedRecipients { get; set; }
    public int SkippedRecipients { get; set; }
}
