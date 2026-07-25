using System.Net.Mail;
using System.Text.Json;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class EmailBroadcastService : IEmailBroadcastService
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyDocumentNumberGenerator _numbers;

    public EmailBroadcastService(IApplicationDbContext context, ICompanyDocumentNumberGenerator numbers)
    {
        _context = context;
        _numbers = numbers;
    }

    public async Task<EmailAudiencePreview> PreviewAsync(BroadcastAudienceSelection audience,
        EmailBroadcastPurpose purpose, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(audience, purpose, cancellationToken);
        return new EmailAudiencePreview(resolved.Members.Count,
            resolved.Members.Count(x => x.Eligible), resolved.Members.Count(x => !x.Eligible),
            resolved.DuplicatesRemoved, resolved.Members.Count > 200, resolved.Members.Take(200).ToList());
    }

    public async Task<EmailBroadcast> CreateAsync(EmailBroadcastCommand command,
        CancellationToken cancellationToken)
    {
        ValidateContent(command);
        var resolved = await ResolveAsync(command.Audience, command.Purpose, cancellationToken);
        if (!resolved.Members.Any(x => x.Eligible))
            throw new ArgumentException("The selected audience contains no eligible email recipients.");
        var campaign = new EmailBroadcast
        {
            CampaignNumber = await _numbers.NextEmailBroadcastAsync(cancellationToken),
            CreatedByUserId = command.CreatedByUserId
        };
        Apply(campaign, command, resolved);
        _context.EmailBroadcasts.Add(campaign);
        AddRecipients(campaign.Id, resolved.Members);
        await _context.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public async Task<EmailBroadcast> UpdateDraftAsync(Guid id, EmailBroadcastCommand command,
        CancellationToken cancellationToken)
    {
        ValidateContent(command);
        var campaign = await _context.EmailBroadcasts.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Email broadcast not found.");
        if (campaign.Status != EmailBroadcastStatus.Draft)
            throw new InvalidOperationException("Only draft broadcasts can be edited.");
        var resolved = await ResolveAsync(command.Audience, command.Purpose, cancellationToken);
        if (!resolved.Members.Any(x => x.Eligible))
            throw new ArgumentException("The selected audience contains no eligible email recipients.");
        var current = await _context.EmailBroadcastRecipients.Where(x => x.EmailBroadcastId == id)
            .ToListAsync(cancellationToken);
        _context.EmailBroadcastRecipients.RemoveRange(current);
        Apply(campaign, command, resolved);
        AddRecipients(campaign.Id, resolved.Members);
        await _context.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public async Task<EmailBroadcast> QueueAsync(Guid id, DateTime? scheduledFor,
        CancellationToken cancellationToken)
    {
        var campaign = await _context.EmailBroadcasts.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Email broadcast not found.");
        if (campaign.Status != EmailBroadcastStatus.Draft)
            throw new InvalidOperationException("Only draft broadcasts can be queued.");
        if (!await _context.EmailBroadcastRecipients.AnyAsync(x => x.EmailBroadcastId == id &&
            x.Status == EmailBroadcastRecipientStatus.Pending, cancellationToken))
            throw new InvalidOperationException("The broadcast has no eligible pending recipients.");
        var now = DateTime.UtcNow;
        campaign.ScheduledFor = scheduledFor?.ToUniversalTime();
        campaign.QueuedAt = now;
        campaign.Status = campaign.ScheduledFor.HasValue && campaign.ScheduledFor > now
            ? EmailBroadcastStatus.Scheduled : EmailBroadcastStatus.Queued;
        await _context.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public async Task<EmailBroadcast> CancelAsync(Guid id, Guid cancelledByUserId, string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A cancellation reason is required.");
        var campaign = await _context.EmailBroadcasts.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Email broadcast not found.");
        if (campaign.Status is not (EmailBroadcastStatus.Draft or EmailBroadcastStatus.Scheduled or EmailBroadcastStatus.Queued))
            throw new InvalidOperationException("This broadcast can no longer be cancelled.");
        var pending = await _context.EmailBroadcastRecipients.Where(x => x.EmailBroadcastId == id &&
            x.Status == EmailBroadcastRecipientStatus.Pending).ToListAsync(cancellationToken);
        foreach (var recipient in pending)
        {
            recipient.Status = EmailBroadcastRecipientStatus.Skipped;
            recipient.FailureReason = "Broadcast cancelled before delivery.";
        }
        campaign.Status = EmailBroadcastStatus.Cancelled;
        campaign.CancelledAt = DateTime.UtcNow;
        campaign.CancelledByUserId = cancelledByUserId;
        campaign.CancellationReason = reason.Trim();
        campaign.PendingRecipients = 0;
        campaign.SkippedRecipients += pending.Count;
        await _context.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    public async Task<EmailBroadcast> RetryFailedAsync(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await _context.EmailBroadcasts.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Email broadcast not found.");
        if (campaign.Status != EmailBroadcastStatus.PartiallyFailed)
            throw new InvalidOperationException("Only a partially failed broadcast can be retried.");
        var failed = await _context.EmailBroadcastRecipients.Where(x => x.EmailBroadcastId == id &&
            x.Status == EmailBroadcastRecipientStatus.Failed).ToListAsync(cancellationToken);
        if (failed.Count == 0) throw new InvalidOperationException("No failed recipients are available to retry.");
        foreach (var recipient in failed)
        {
            recipient.Status = EmailBroadcastRecipientStatus.Pending;
            recipient.FailureReason = null;
        }
        campaign.Status = EmailBroadcastStatus.Queued;
        campaign.QueuedAt = DateTime.UtcNow;
        campaign.CompletedAt = null;
        campaign.PendingRecipients = failed.Count;
        campaign.FailedRecipients = 0;
        await _context.SaveChangesAsync(cancellationToken);
        return campaign;
    }

    private async Task<ResolvedAudience> ResolveAsync(BroadcastAudienceSelection selection,
        EmailBroadcastPurpose purpose, CancellationToken cancellationToken)
    {
        ValidateSelection(selection);
        var candidates = new List<AudienceCandidate>();

        if (selection.AllClients || selection.CustomerIds.Count > 0)
        {
            var query = _context.Customers.AsNoTracking().AsQueryable();
            if (!selection.AllClients) query = query.Where(x => selection.CustomerIds.Contains(x.Id));
            candidates.AddRange(await query.Select(x => new AudienceCandidate(EmailRecipientSourceType.Client,
                x.Id, x.FullName, x.Email ?? string.Empty)).ToListAsync(cancellationToken));
        }

        if (selection.AllContacts || selection.ContactIds.Count > 0 || selection.ContactTypes.Count > 0)
        {
            var query = _context.Contacts.AsNoTracking().Where(x => x.Status == ContactStatus.Active);
            if (!selection.AllContacts)
                query = query.Where(x => selection.ContactIds.Contains(x.Id) || selection.ContactTypes.Contains(x.Type));
            candidates.AddRange(await query.Select(x => new AudienceCandidate(EmailRecipientSourceType.Contact,
                x.Id, x.FullName, x.Email)).ToListAsync(cancellationToken));
        }

        if (selection.AllSuppliers || selection.SupplierIds.Count > 0)
        {
            var query = _context.Suppliers.AsNoTracking().Where(x => x.IsActive);
            if (!selection.AllSuppliers) query = query.Where(x => selection.SupplierIds.Contains(x.Id));
            candidates.AddRange(await query.Select(x => new AudienceCandidate(EmailRecipientSourceType.Supplier,
                x.Id, x.Name, x.Email)).ToListAsync(cancellationToken));
        }

        if (selection.AllActiveStaff || selection.StaffUserIds.Count > 0)
        {
            var query = from user in _context.UserAccounts.AsNoTracking()
                        join profile in _context.StaffProfiles.AsNoTracking() on user.Id equals profile.UserAccountId
                        where user.Role != UserRole.Customer && user.Status == AccountStatus.Active &&
                              profile.Status == StaffStatus.Active
                        select new { user.Id, user.FullName, user.Email };
            if (!selection.AllActiveStaff) query = query.Where(x => selection.StaffUserIds.Contains(x.Id));
            candidates.AddRange(await query.Select(x => new AudienceCandidate(EmailRecipientSourceType.Staff,
                x.Id, x.FullName, x.Email)).ToListAsync(cancellationToken));
        }

        var exclusions = selection.ExcludeEmails.Select(ClientIdentityNormalizer.Email)
            .Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
        var customerPreferences = await (from customer in _context.Customers.AsNoTracking()
                                         join preference in _context.NotificationPreferences.AsNoTracking()
                                             on customer.Id equals preference.CustomerId into preferences
                                         from preference in preferences.DefaultIfEmpty()
                                         select new { customer.NormalizedEmail,
                                             EmailEnabled = preference == null || preference.EmailEnabled,
                                             OperationalEnabled = preference == null || preference.OperationalUpdates,
                                             MarketingEnabled = preference != null && preference.MarketingUpdates })
            .ToDictionaryAsync(x => x.NormalizedEmail, x => x, cancellationToken);

        var ordered = candidates.OrderBy(x => x.SourceType).ThenBy(x => x.Name).ToList();
        var members = new List<EmailAudienceMember>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = 0;
        foreach (var candidate in ordered)
        {
            var normalized = ClientIdentityNormalizer.Email(candidate.Email);
            if (!seen.Add(normalized)) { duplicates++; continue; }
            string? reason = null;
            if (normalized.Length == 0 || !MailAddress.TryCreate(candidate.Email, out _))
                reason = "Missing or invalid email address.";
            else if (exclusions.Contains(normalized)) reason = "Explicitly excluded from this broadcast.";
            else if (customerPreferences.TryGetValue(normalized, out var preference) &&
                (!preference.EmailEnabled || purpose == EmailBroadcastPurpose.Operational && !preference.OperationalEnabled ||
                 purpose == EmailBroadcastPurpose.Marketing && !preference.MarketingEnabled))
                reason = purpose == EmailBroadcastPurpose.Marketing
                    ? "Client has not consented to marketing email." : "Client email preference disables this message type.";
            members.Add(new EmailAudienceMember(candidate.SourceType, candidate.SourceId, candidate.Name,
                candidate.Email.Trim().ToLowerInvariant(), reason is null, reason));
        }
        return new ResolvedAudience(members, duplicates);
    }

    private void AddRecipients(Guid broadcastId, IEnumerable<EmailAudienceMember> members)
    {
        foreach (var member in members)
            _context.EmailBroadcastRecipients.Add(new EmailBroadcastRecipient
            {
                EmailBroadcastId = broadcastId, SourceType = member.SourceType, SourceId = member.SourceId,
                RecipientName = member.Name.Trim(), Email = member.Email,
                NormalizedEmail = ClientIdentityNormalizer.Email(member.Email),
                Status = member.Eligible ? EmailBroadcastRecipientStatus.Pending : EmailBroadcastRecipientStatus.Skipped,
                FailureReason = member.SkipReason
            });
    }

    private static void Apply(EmailBroadcast campaign, EmailBroadcastCommand command,
        ResolvedAudience audience)
    {
        campaign.Name = command.Name.Trim(); campaign.Subject = command.Subject.Trim();
        campaign.Body = command.Body.Trim(); campaign.ReplyTo = Clean(command.ReplyTo);
        campaign.Purpose = command.Purpose;
        campaign.AudienceSelectionJson = JsonSerializer.Serialize(command.Audience);
        campaign.TotalRecipients = audience.Members.Count;
        campaign.PendingRecipients = audience.Members.Count(x => x.Eligible);
        campaign.SentRecipients = 0; campaign.FailedRecipients = 0;
        campaign.SkippedRecipients = audience.Members.Count(x => !x.Eligible);
    }

    private static void ValidateContent(EmailBroadcastCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 160)
            throw new ArgumentException("Campaign name is required and cannot exceed 160 characters.");
        if (string.IsNullOrWhiteSpace(command.Subject) || command.Subject.Trim().Length > 250)
            throw new ArgumentException("Email subject is required and cannot exceed 250 characters.");
        if (string.IsNullOrWhiteSpace(command.Body) || command.Body.Trim().Length > 20_000)
            throw new ArgumentException("Email body is required and cannot exceed 20,000 characters.");
        if (!string.IsNullOrWhiteSpace(command.ReplyTo) && !MailAddress.TryCreate(command.ReplyTo, out _))
            throw new ArgumentException("Reply-to email address is invalid.");
    }

    private static void ValidateSelection(BroadcastAudienceSelection selection)
    {
        if (!selection.AllClients && !selection.AllContacts && !selection.AllSuppliers &&
            !selection.AllActiveStaff && selection.CustomerIds.Count == 0 && selection.ContactIds.Count == 0 &&
            selection.SupplierIds.Count == 0 && selection.StaffUserIds.Count == 0 &&
            selection.ContactTypes.Count == 0)
            throw new ArgumentException("Select at least one audience group or individual recipient.");
        if (selection.CustomerIds.Count + selection.ContactIds.Count + selection.SupplierIds.Count +
            selection.StaffUserIds.Count > 10_000)
            throw new ArgumentException("A single audience selection supports at most 10,000 explicit records.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record AudienceCandidate(EmailRecipientSourceType SourceType, Guid SourceId,
        string Name, string Email);
    private sealed record ResolvedAudience(IReadOnlyList<EmailAudienceMember> Members, int DuplicatesRemoved);
}
