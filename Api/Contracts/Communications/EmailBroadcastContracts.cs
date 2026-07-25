using Domain.Enums;

namespace Api.Controllers;

public sealed record BroadcastAudienceRequest(
    bool AllClients = false,
    bool AllContacts = false,
    bool AllSuppliers = false,
    bool AllActiveStaff = false,
    IReadOnlyList<Guid>? CustomerIds = null,
    IReadOnlyList<Guid>? ContactIds = null,
    IReadOnlyList<Guid>? SupplierIds = null,
    IReadOnlyList<Guid>? StaffUserIds = null,
    IReadOnlyList<ContactType>? ContactTypes = null,
    IReadOnlyList<string>? ExcludeEmails = null);

public sealed record PreviewBroadcastAudienceRequest(EmailBroadcastPurpose Purpose,
    BroadcastAudienceRequest Audience);

public sealed record CreateEmailBroadcastRequest(string Name, string Subject, string Body,
    string? ReplyTo, EmailBroadcastPurpose Purpose, BroadcastAudienceRequest Audience);

public sealed record QueueEmailBroadcastRequest(DateTime? ScheduledFor);
public sealed record CancelEmailBroadcastRequest(string Reason);
public sealed record AddBroadcastAttachmentRequest(string FileName, string ContentType,
    long SizeBytes, string ContentBase64);
