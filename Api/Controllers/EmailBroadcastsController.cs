using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/communications/broadcasts")]
public sealed class EmailBroadcastsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailBroadcastService _broadcasts;
    private readonly IIntegrationGateway _integrations;

    public EmailBroadcastsController(IApplicationDbContext context,
        IEmailBroadcastService broadcasts, IIntegrationGateway integrations)
    {
        _context = context; _broadcasts = broadcasts; _integrations = integrations;
    }

    [HttpPost("audience/preview")]
    public async Task<ActionResult> Preview(PreviewBroadcastAudienceRequest request,
        CancellationToken cancellationToken) => Ok(await _broadcasts.PreviewAsync(
            ToSelection(request.Audience), request.Purpose, cancellationToken));

    [HttpPost]
    public async Task<ActionResult> Create(CreateEmailBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        var campaign = await _broadcasts.CreateAsync(ToCommand(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = campaign.Id }, CampaignView(campaign));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, CreateEmailBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        var campaign = await _broadcasts.UpdateDraftAsync(id, ToCommand(request), cancellationToken);
        return Ok(CampaignView(campaign));
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] EmailBroadcastStatus? status,
        [FromQuery] EmailBroadcastPurpose? purpose, [FromQuery] string? search,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.EmailBroadcasts.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (purpose.HasValue) query = query.Where(x => x.Purpose == purpose);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.CampaignNumber.ToLower().Contains(value) ||
                x.Name.ToLower().Contains(value) || x.Subject.ToLower().Contains(value));
        }
        var projected = query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new EmailBroadcastListRow
            {
                Id = x.Id, CampaignNumber = x.CampaignNumber, Name = x.Name, Subject = x.Subject,
                Purpose = x.Purpose, Status = x.Status, ScheduledFor = x.ScheduledFor,
                TotalRecipients = x.TotalRecipients, PendingRecipients = x.PendingRecipients,
                SentRecipients = x.SentRecipients, FailedRecipients = x.FailedRecipients,
                SkippedRecipients = x.SkippedRecipients, CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt
            });
        return Ok(await projected.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await _context.EmailBroadcasts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken);
        if (campaign is null) return NotFound();
        var attachments = await _context.EmailBroadcastAttachments.AsNoTracking()
            .Where(x => x.EmailBroadcastId == id).OrderBy(x => x.FileName)
            .Select(x => new
            {
                x.Id, x.EmailBroadcastId, x.FileName, x.ContentType, x.SizeBytes,
                x.StorageKey, x.Checksum, x.CreatedAt
            }).ToListAsync(cancellationToken);
        var statusBreakdown = await _context.EmailBroadcastRecipients.AsNoTracking()
            .Where(x => x.EmailBroadcastId == id).GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(cancellationToken);
        return Ok(new { Campaign = CampaignView(campaign), Attachments = attachments,
            RecipientStatus = statusBreakdown, ProviderMode = "Configured" });
    }

    [HttpGet("{id:guid}/recipients")]
    public async Task<ActionResult> Recipients(Guid id,
        [FromQuery] EmailBroadcastRecipientStatus? status,
        [FromQuery] EmailRecipientSourceType? sourceType, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        if (!await _context.EmailBroadcasts.AnyAsync(x => x.Id == id, cancellationToken)) return NotFound();
        var query = _context.EmailBroadcastRecipients.AsNoTracking().Where(x => x.EmailBroadcastId == id);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (sourceType.HasValue) query = query.Where(x => x.SourceType == sourceType);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.RecipientName.ToLower().Contains(value) ||
                x.Email.ToLower().Contains(value));
        }
        return Ok(await query.OrderBy(x => x.RecipientName).ThenBy(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult> AddAttachment(Guid id, AddBroadcastAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        var campaign = await _context.EmailBroadcasts.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Email broadcast not found.");
        if (campaign.Status != EmailBroadcastStatus.Draft)
            return Conflict("Attachments can only be changed while the broadcast is a draft.");
        var contentType = request.ContentType.Trim().ToLowerInvariant();
        if (!contentType.StartsWith("image/") && contentType != "application/pdf")
            return BadRequest("Broadcast attachments must be images or PDF documents.");
        if (request.SizeBytes <= 0 || request.SizeBytes > 20_000_000)
            return BadRequest("Attachment size must be between 1 byte and 20 MB.");
        byte[] content;
        try
        {
            content = Convert.FromBase64String(request.ContentBase64);
        }
        catch (FormatException)
        {
            return BadRequest("Attachment content is not valid base64 data.");
        }
        if (content.LongLength != request.SizeBytes)
            return BadRequest("Attachment size does not match the uploaded content.");
        if (!HasExpectedSignature(contentType, content))
            return BadRequest("Attachment content does not match its declared image or PDF type.");
        var existing = await _context.EmailBroadcastAttachments.Where(x => x.EmailBroadcastId == id)
            .ToListAsync(cancellationToken);
        if (existing.Count >= 10 || existing.Sum(x => x.SizeBytes) + request.SizeBytes > 25_000_000)
            return BadRequest("A broadcast supports at most 10 attachments and 25 MB combined.");
        if (existing.Any(x => x.FileName.ToLower() == request.FileName.Trim().ToLower()))
            return Conflict("An attachment with this file name already exists on the broadcast.");
        var stored = await _integrations.StoreDocumentAsync(request.FileName.Trim(), contentType,
            request.SizeBytes, cancellationToken);
        var attachment = new EmailBroadcastAttachment
        {
            EmailBroadcastId = id, FileName = request.FileName.Trim(), ContentType = contentType,
            SizeBytes = request.SizeBytes, StorageKey = stored.StorageKey,
            Checksum = Convert.ToHexString(SHA256.HashData(content)), Content = content
        };
        _context.EmailBroadcastAttachments.Add(attachment);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/communications/broadcasts/{id}/attachments/{attachment.Id}",
            new
            {
                Attachment = new
                {
                    attachment.Id, attachment.EmailBroadcastId, attachment.FileName,
                    attachment.ContentType, attachment.SizeBytes, attachment.StorageKey,
                    attachment.Checksum, attachment.CreatedAt
                },
                stored.DownloadUrl,
                ProviderMode = "Configured"
            });
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> RemoveAttachment(Guid id, Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var campaign = await _context.EmailBroadcasts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Email broadcast not found.");
        if (campaign.Status != EmailBroadcastStatus.Draft)
            return Conflict("Attachments can only be changed while the broadcast is a draft.");
        var attachment = await _context.EmailBroadcastAttachments.FirstOrDefaultAsync(x =>
            x.Id == attachmentId && x.EmailBroadcastId == id, cancellationToken)
            ?? throw new KeyNotFoundException("Broadcast attachment not found.");
        _context.EmailBroadcastAttachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/queue")]
    public async Task<ActionResult> Queue(Guid id, QueueEmailBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        var campaign = await _broadcasts.QueueAsync(id, request.ScheduledFor, cancellationToken);
        return Accepted($"/api/communications/broadcasts/{id}", CampaignView(campaign));
    }

    [HttpPost("{id:guid}/retry-failed")]
    public async Task<ActionResult> RetryFailed(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await _broadcasts.RetryFailedAsync(id, cancellationToken);
        return Accepted($"/api/communications/broadcasts/{id}", CampaignView(campaign));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, CancelEmailBroadcastRequest request,
        CancellationToken cancellationToken) => Ok(CampaignView(await _broadcasts.CancelAsync(
            id, CurrentUserId, request.Reason, cancellationToken)));

    private EmailBroadcastCommand ToCommand(CreateEmailBroadcastRequest request) => new(
        request.Name, request.Subject, request.Body, request.ReplyTo, request.Purpose,
        ToSelection(request.Audience), CurrentUserId);

    private static BroadcastAudienceSelection ToSelection(BroadcastAudienceRequest request) => new(
        request.AllClients, request.AllContacts, request.AllSuppliers, request.AllActiveStaff,
        request.CustomerIds ?? Array.Empty<Guid>(), request.ContactIds ?? Array.Empty<Guid>(),
        request.SupplierIds ?? Array.Empty<Guid>(), request.StaffUserIds ?? Array.Empty<Guid>(),
        request.ContactTypes ?? Array.Empty<ContactType>(), request.ExcludeEmails ?? Array.Empty<string>());

    private static object CampaignView(EmailBroadcast campaign) => new
    {
        campaign.Id, campaign.CampaignNumber, campaign.Name, campaign.Subject, campaign.Body,
        campaign.ReplyTo, campaign.Purpose, campaign.Status, campaign.ScheduledFor,
        campaign.QueuedAt, campaign.StartedAt, campaign.CompletedAt, campaign.CancelledAt,
        campaign.CancellationReason, campaign.TotalRecipients, campaign.PendingRecipients,
        campaign.SentRecipients, campaign.FailedRecipients, campaign.SkippedRecipients,
        campaign.CreatedByUserId, campaign.CreatedAt
    };

    private static bool HasExpectedSignature(string contentType, byte[] content)
    {
        if (content.Length < 4) return false;
        return contentType switch
        {
            "application/pdf" => content.AsSpan().StartsWith("%PDF"u8),
            "image/png" => content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            "image/jpeg" => content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            "image/gif" => content.AsSpan().StartsWith("GIF8"u8),
            "image/webp" => content.Length >= 12 &&
                content.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    private sealed class EmailBroadcastListRow
    {
        public Guid Id { get; init; }
        public string CampaignNumber { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public EmailBroadcastPurpose Purpose { get; init; }
        public EmailBroadcastStatus Status { get; init; }
        public DateTime? ScheduledFor { get; init; }
        public int TotalRecipients { get; init; }
        public int PendingRecipients { get; init; }
        public int SentRecipients { get; init; }
        public int FailedRecipients { get; init; }
        public int SkippedRecipients { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
    }
}
