using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class EmailBroadcastRecipient : BaseEntity
{
    public Guid EmailBroadcastId { get; set; }
    public EmailRecipientSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public EmailBroadcastRecipientStatus Status { get; set; } = EmailBroadcastRecipientStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? FailureReason { get; set; }
    public string? ProviderReference { get; set; }
}
