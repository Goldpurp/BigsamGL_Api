using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces;

public sealed record BroadcastAudienceSelection(
    bool AllClients,
    bool AllContacts,
    bool AllSuppliers,
    bool AllActiveStaff,
    IReadOnlyCollection<Guid> CustomerIds,
    IReadOnlyCollection<Guid> ContactIds,
    IReadOnlyCollection<Guid> SupplierIds,
    IReadOnlyCollection<Guid> StaffUserIds,
    IReadOnlyCollection<ContactType> ContactTypes,
    IReadOnlyCollection<string> ExcludeEmails);

public sealed record EmailAudienceMember(EmailRecipientSourceType SourceType, Guid SourceId,
    string Name, string Email, bool Eligible, string? SkipReason);

public sealed record EmailAudiencePreview(int TotalUniqueRecipients, int EligibleRecipients,
    int SkippedRecipients, int DuplicatesRemoved, bool SampleTruncated,
    IReadOnlyList<EmailAudienceMember> Sample);

public sealed record EmailBroadcastCommand(string Name, string Subject, string Body,
    string? ReplyTo, EmailBroadcastPurpose Purpose, BroadcastAudienceSelection Audience,
    Guid CreatedByUserId);

public interface IEmailBroadcastService
{
    Task<EmailAudiencePreview> PreviewAsync(BroadcastAudienceSelection audience,
        EmailBroadcastPurpose purpose, CancellationToken cancellationToken);
    Task<EmailBroadcast> CreateAsync(EmailBroadcastCommand command, CancellationToken cancellationToken);
    Task<EmailBroadcast> UpdateDraftAsync(Guid id, EmailBroadcastCommand command,
        CancellationToken cancellationToken);
    Task<EmailBroadcast> QueueAsync(Guid id, DateTime? scheduledFor, CancellationToken cancellationToken);
    Task<EmailBroadcast> CancelAsync(Guid id, Guid cancelledByUserId, string reason,
        CancellationToken cancellationToken);
    Task<EmailBroadcast> RetryFailedAsync(Guid id, CancellationToken cancellationToken);
}
