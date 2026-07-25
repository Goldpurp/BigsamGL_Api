using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Exceptions;
using Api.Services;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Authorize(Policy = "OperationsTeam")]
[Route("api/operations/tracking")]
public sealed class ManifestTrackingController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    private readonly IClientCommunicationService _communications;
    private readonly IManifestTrackingIngestionService _ingestion;

    public ManifestTrackingController(ApplicationDbContext context, IActiveBranchContext branches,
        IClientCommunicationService communications,
        IManifestTrackingIngestionService ingestion)
    {
        _context = context;
        _branches = branches;
        _communications = communications;
        _ingestion = ingestion;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search,
        [FromQuery] ManifestTrackingStatus? status, [FromQuery] string? provider,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = from connection in _context.ManifestTrackingConnections.AsNoTracking()
                    join manifest in _context.OperationalManifests.AsNoTracking()
                        on connection.ManifestId equals manifest.Id
                    where branch.IsAllBranches || connection.BranchId == branch.BranchId
                    select new { Connection = connection, Manifest = manifest };
        if (status.HasValue) query = query.Where(x => x.Connection.Status == status);
        if (!string.IsNullOrWhiteSpace(provider))
        { var value = provider.Trim().ToUpperInvariant(); query = query.Where(x => x.Connection.Provider == value); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Connection.ExternalReference.ToLower().Contains(value) ||
                x.Connection.ShippingCompanyName.ToLower().Contains(value) ||
                x.Manifest.ManifestNumber.ToLower().Contains(value) || x.Manifest.ShipmentId.ToLower().Contains(value));
        }
        var result = await query.OrderByDescending(x => x.Connection.LastSuccessfulSyncAt)
            .ThenByDescending(x => x.Connection.Id).ToPagedResponseAsync(page, pageSize, true, cancellationToken);
        var branchKeys = await BranchKeysAsync(result.Items.Select(x => x.Connection.BranchId), cancellationToken);
        return Ok(new PagedResponse<TrackingConnectionResponse>(result.Items.Select(x => MapConnection(
            x.Connection, x.Manifest, branchKeys[x.Connection.BranchId], null)).ToList(), result.Page,
            result.PageSize, result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
    }

    [HttpGet("{manifestId:guid}")]
    [HttpGet("/api/operations/manifests/{manifestId:guid}/tracking")]
    public async Task<ActionResult> Get(Guid manifestId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var (connection, manifest, branch) = await ScopedAsync(manifestId, false, cancellationToken);
        var events = await _context.ManifestTrackingEvents.AsNoTracking().Where(x =>
                x.ConnectionId == connection.Id)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, true, cancellationToken);
        Response.Headers.ETag = Quote(connection.RowVersion);
        return Ok(new { Connection = MapConnection(connection, manifest, branch.BranchKey!, null),
            Events = new PagedResponse<TrackingEventResponse>(events.Items.Select(MapEvent).ToList(),
                events.Page, events.PageSize, events.HasPrevious, events.HasNext, events.Total, events.TotalPages) });
    }

    [HttpPut("{manifestId:guid}/connection")]
    public async Task<ActionResult<TrackingConnectionResponse>> Connect(Guid manifestId,
        TrackingConnectionRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var key = IdempotencyKey();
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.ShippingCompanyName) ||
            string.IsNullOrWhiteSpace(request.ExternalReference) || string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("TRACKING_CONNECTION_REQUIRED", "Provider, shipping company, external reference and reason are required.");
        var manifest = await _context.OperationalManifests.FirstOrDefaultAsync(x => x.Id == manifestId &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Manifest not found.");
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "tracking.connection" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null)
        {
            var replay = await _context.ManifestTrackingConnections.AsNoTracking().SingleAsync(x =>
                x.Id == prior.ResourceId && x.BranchId == branch.BranchId, cancellationToken);
            return Ok(MapConnection(replay, manifest, branch.BranchKey!, null));
        }
        var provider = request.Provider.Trim().ToUpperInvariant();
        var reference = NormalizeReference(provider, request.ExternalReference, request.ReferenceType);
        var connection = await _context.ManifestTrackingConnections.FirstOrDefaultAsync(x =>
            x.ManifestId == manifestId, cancellationToken);
        string? publicToken = null;
        if (connection is null)
        {
            publicToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            connection = new ManifestTrackingConnection { BranchId = branch.BranchId!.Value,
                ManifestId = manifestId, PublicTokenHash = Hash(publicToken) };
            _context.ManifestTrackingConnections.Add(connection);
        }
        else
        {
            var version = Version(); CheckVersion(connection.RowVersion, version, "tracking connection");
            connection.RowVersion++;
        }
        connection.Provider = provider; connection.ShippingCompanyName = request.ShippingCompanyName.Trim();
        connection.ExternalReference = reference; connection.ReferenceType = request.ReferenceType;
        connection.Status = ManifestTrackingStatus.Connected;
        connection.NextSyncAt = DateTime.UtcNow;
        manifest.CarrierName = connection.ShippingCompanyName; manifest.CarrierReference = reference;
        manifest.TrackingProvider = provider; manifest.ContainerOrFlightNumber = reference;
        manifest.RowVersion++; manifest.LastEditedByUserId = CurrentUserId;
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "tracking.connection", Key = key, ResourceType = nameof(ManifestTrackingConnection),
            ResourceId = connection.Id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken);
        var response = MapConnection(connection, manifest, branch.BranchKey!, publicToken);
        Response.Headers.ETag = Quote(connection.RowVersion); return Ok(response);
    }

    [HttpPost("{manifestId:guid}/sync")]
    [EnableRateLimiting("CarrierSync")]
    public async Task<ActionResult<CarrierTrackingSyncResponse>> Sync(Guid manifestId,
        CancellationToken cancellationToken)
    {
        var key = IdempotencyKey(); var (connection, manifest, _) = await ScopedAsync(manifestId, true, cancellationToken);
        if (!connection.Provider.Equals("MSC", StringComparison.OrdinalIgnoreCase))
            throw Rule("TRACKING_PROVIDER_NOT_SUPPORTED",
                "Direct pull tracking is currently available for MSC. Use the carrier webhook or a manual event for other providers.");
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "tracking.sync" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null)
            return Ok(new CarrierTrackingSyncResponse(manifest.Id, manifest.ManifestNumber,
                connection.Provider, connection.ExternalReference, connection.ReferenceType,
                0, 0, 0, false, true, connection.LastSuccessfulSyncAt ?? prior.CreatedAt, []));

        var result = await _ingestion.SyncAsync(connection.Id, cancellationToken);
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            ActorUserId = CurrentUserId,
            Operation = "tracking.sync",
            Key = key,
            ResourceType = nameof(ManifestTrackingConnection),
            ResourceId = connection.Id,
            ExpiresAt = result.SyncedAt.AddHours(24)
        });
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new CarrierTrackingSyncResponse(manifest.Id, manifest.ManifestNumber,
            connection.Provider, connection.ExternalReference, connection.ReferenceType,
            result.EventsReceived, result.NewEventsStored, result.DuplicatesIgnored, result.IsMock,
            false, result.SyncedAt, result.NewEvents.Select(MapEvent).ToList()));
    }

    [HttpPost("{manifestId:guid}/manual-events")]
    public async Task<ActionResult<TrackingEventResponse>> Manual(Guid manifestId,
        ManualTrackingEventRequest request, CancellationToken cancellationToken)
    {
        var key = IdempotencyKey(); var (connection, _, branch) = await ScopedAsync(manifestId, true, cancellationToken);
        ValidateEvent(request.Title, request.Description, request.OccurredAt, request.Reason);
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "tracking.manual" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null) return Ok(MapEvent(await _context.ManifestTrackingEvents.AsNoTracking()
            .SingleAsync(x => x.Id == prior.ResourceId && x.BranchId == branch.BranchId, cancellationToken)));
        var value = new ManifestTrackingEvent { BranchId = branch.BranchId!.Value,
            ConnectionId = connection.Id, Source = TrackingEventSource.Staff,
            InternalTitle = request.Title.Trim(), InternalDescription = request.Description.Trim(),
            PublicTitle = request.Title.Trim(), PublicDescription = request.Description.Trim(),
            Place = Null(request.Place), OccurredAt = request.OccurredAt.ToUniversalTime(),
            EstimatedAt = request.EstimatedAt?.ToUniversalTime() };
        _context.ManifestTrackingEvents.Add(value);
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "tracking.manual", Key = key, ResourceType = nameof(ManifestTrackingEvent),
            ResourceId = value.Id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken); Response.Headers.ETag = Quote(value.RowVersion);
        return CreatedAtAction(nameof(Get), new { manifestId }, MapEvent(value));
    }

    [HttpPost("{manifestId:guid}/events/{eventId:guid}/review")]
    public async Task<ActionResult<TrackingEventResponse>> Review(Guid manifestId, Guid eventId,
        ReviewTrackingEventRequest request, CancellationToken cancellationToken)
    {
        var (connection, _, branch) = await ScopedAsync(manifestId, true, cancellationToken);
        var version = Version();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Approve &&
            (string.IsNullOrWhiteSpace(request.PublicTitle) || string.IsNullOrWhiteSpace(request.PublicDescription)))
            throw Rule("REVIEW_DETAILS_REQUIRED", "Approved events require public wording and a review reason.");
        var value = await _context.ManifestTrackingEvents.FirstOrDefaultAsync(x => x.Id == eventId &&
            x.ConnectionId == connection.Id && x.BranchId == branch.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException("Tracking event not found.");
        CheckVersion(value.RowVersion, version, "tracking event");
        if (value.ReviewState == TrackingEventReviewState.Published)
            throw Rule("EVENT_ALREADY_PUBLISHED", "Published tracking history cannot be rewritten.");
        value.ReviewState = request.Approve ? TrackingEventReviewState.Reviewed : TrackingEventReviewState.Rejected;
        value.PublicTitle = request.Approve ? request.PublicTitle.Trim() : null;
        value.PublicDescription = request.Approve ? request.PublicDescription.Trim() : null;
        value.Place = request.Approve ? Null(request.Place) : value.Place;
        value.EstimatedAt = request.Approve ? request.EstimatedAt?.ToUniversalTime() : value.EstimatedAt;
        value.ReviewedAt = DateTime.UtcNow; value.ReviewedByUserId = CurrentUserId; value.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken); Response.Headers.ETag = Quote(value.RowVersion);
        return Ok(MapEvent(value));
    }

    [HttpPost("{manifestId:guid}/events/{eventId:guid}/publish")]
    public async Task<ActionResult<TrackingEventResponse>> Publish(Guid manifestId, Guid eventId,
        PublishTrackingEventRequest request, CancellationToken cancellationToken)
    {
        var key = IdempotencyKey(); var (connection, _, branch) = await ScopedAsync(manifestId, true, cancellationToken);
        if (!request.Confirmation.Equals("PUBLISH", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("PUBLISH_CONFIRMATION_REQUIRED", "Type PUBLISH and provide a publication reason.");
        var prior = await _context.IdempotencyRecords.AsNoTracking().AnyAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "tracking.publish" && x.Key == key &&
            x.ResourceId == eventId && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        var value = await _context.ManifestTrackingEvents.FirstOrDefaultAsync(x => x.Id == eventId &&
            x.ConnectionId == connection.Id && x.BranchId == branch.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException("Tracking event not found.");
        if (prior) return Ok(MapEvent(value));
        CheckVersion(value.RowVersion, Version(), "tracking event");
        if (value.ReviewState != TrackingEventReviewState.Reviewed)
            throw Rule("EVENT_NOT_REVIEWED", "Review the customer-safe wording before publication.");
        value.ReviewState = TrackingEventReviewState.Published; value.PublishedAt = DateTime.UtcNow;
        value.PublishedByUserId = CurrentUserId; value.RowVersion++;
        connection.LastKnownPlace = value.Place; connection.EstimatedAt = value.EstimatedAt;
        connection.RowVersion++;
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "tracking.publish", Key = key, ResourceType = nameof(ManifestTrackingEvent),
            ResourceId = value.Id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken); return Ok(MapEvent(value));
    }

    [HttpPost("{manifestId:guid}/notify-affected-clients")]
    public async Task<ActionResult> Notify(Guid manifestId, NotifyTrackingClientsRequest request,
        CancellationToken cancellationToken)
    {
        var key = IdempotencyKey(); var (connection, manifest, _) = await ScopedAsync(manifestId, true, cancellationToken);
        if (!request.Confirmation.Equals("NOTIFY", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            throw Rule("NOTIFY_CONFIRMATION_REQUIRED", "Type NOTIFY and provide subject and body.");
        var prior = await _context.IdempotencyRecords.AsNoTracking().AnyAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "tracking.notify" && x.Key == key &&
            x.ResourceId == manifestId && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior) return Accepted(new { manifestId, replayed = true, deliveryQueued = true });
        var latest = await _context.ManifestTrackingEvents.AsNoTracking().Where(x =>
                x.ConnectionId == connection.Id && x.ReviewState == TrackingEventReviewState.Published)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw Rule("NO_PUBLISHED_UPDATE", "Publish at least one approved event before notifying clients.");
        var clientIds = await _context.OperationalManifestEntries.AsNoTracking().Where(x => x.ManifestId == manifestId)
            .Select(x => x.CustomerId).Distinct().ToListAsync(cancellationToken);
        foreach (var clientId in clientIds)
            await _communications.SendAsync(clientId, CommunicationType.TrackingUpdate,
                request.Subject.Trim(), request.Body.Trim(), nameof(OperationalManifest), manifestId, [],
                new { manifest.ManifestNumber, manifest.ShipmentId, connection.ShippingCompanyName,
                    Event = new { latest.PublicTitle, latest.PublicDescription, latest.Place,
                        latest.OccurredAt, latest.EstimatedAt } }, cancellationToken);
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "tracking.notify", Key = key, ResourceType = nameof(OperationalManifest),
            ResourceId = manifestId, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken);
        return Accepted(new { manifestId, recipients = clientIds.Count, deliveryQueued = true });
    }

    [HttpPatch("/api/operations/manifests/{manifestId:guid}/client-tracking-update")]
    public async Task<ActionResult<TrackingEventResponse>> ClientTrackingUpdate(Guid manifestId,
        ClientTrackingUpdateRequest request, CancellationToken cancellationToken)
    {
        var key = IdempotencyKey();
        var (connection, _, branch) = await ScopedAsync(manifestId, true, cancellationToken);
        CheckVersion(connection.RowVersion, Version(), "tracking connection");
        ValidateEvent(request.CustomerVisibleStatus, request.CustomerVisibleMessage,
            request.OccurredAt, request.Reason);
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "tracking.client-update" &&
            x.Key == key && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null)
            return Ok(MapEvent(await _context.ManifestTrackingEvents.AsNoTracking().SingleAsync(x =>
                x.Id == prior.ResourceId, cancellationToken)));
        var now = DateTime.UtcNow;
        var value = new ManifestTrackingEvent
        {
            BranchId = branch.BranchId!.Value,
            ConnectionId = connection.Id,
            Source = TrackingEventSource.Staff,
            ReviewState = request.ShowToCustomer
                ? TrackingEventReviewState.Published : TrackingEventReviewState.Draft,
            InternalTitle = request.CustomerVisibleStatus.Trim(),
            InternalDescription = request.CustomerVisibleMessage.Trim(),
            PublicTitle = request.ShowToCustomer ? request.CustomerVisibleStatus.Trim() : null,
            PublicDescription = request.ShowToCustomer ? request.CustomerVisibleMessage.Trim() : null,
            Place = Null(request.Place),
            OccurredAt = request.OccurredAt.ToUniversalTime(),
            EstimatedAt = request.EstimatedAt?.ToUniversalTime(),
            ReviewedAt = request.ShowToCustomer ? now : null,
            ReviewedByUserId = request.ShowToCustomer ? CurrentUserId : null,
            PublishedAt = request.ShowToCustomer ? now : null,
            PublishedByUserId = request.ShowToCustomer ? CurrentUserId : null
        };
        _context.ManifestTrackingEvents.Add(value);
        if (request.ShowToCustomer)
        {
            connection.LastKnownPlace = value.Place;
            connection.EstimatedAt = value.EstimatedAt;
        }
        connection.RowVersion++;
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            ActorUserId = CurrentUserId, Operation = "tracking.client-update", Key = key,
            ResourceType = nameof(ManifestTrackingEvent), ResourceId = value.Id,
            ExpiresAt = now.AddHours(24)
        });
        await _context.SaveChangesAsync(cancellationToken);
        Response.Headers.ETag = Quote(value.RowVersion);
        return CreatedAtAction(nameof(Get), new { manifestId }, MapEvent(value));
    }

    private async Task<(ManifestTrackingConnection Connection, OperationalManifest Manifest,
        ActiveBranchScope Branch)> ScopedAsync(Guid manifestId, bool mutation, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(mutation, cancellationToken);
        var row = await (from connection in _context.ManifestTrackingConnections
                         join manifest in _context.OperationalManifests on connection.ManifestId equals manifest.Id
                         where connection.ManifestId == manifestId &&
                             (branch.IsAllBranches || connection.BranchId == branch.BranchId)
                         select new { Connection = connection, Manifest = manifest })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("Manifest tracking connection not found.");
        return (row.Connection, row.Manifest, branch);
    }

    private async Task<Dictionary<Guid, string>> BranchKeysAsync(IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var values = ids.Distinct().ToList();
        return (await _context.Branches.AsNoTracking().Where(x => values.Contains(x.Id))
            .Select(x => new { x.Id, x.Code }).ToListAsync(cancellationToken)).ToDictionary(x => x.Id,
            x => x.Code switch { "UK-MAN" => "manchester", "NG-LOS" => "lagos",
                "CN-GZ" => "guangzhou", _ => x.Code.ToLowerInvariant() });
    }

    private static TrackingConnectionResponse MapConnection(ManifestTrackingConnection x,
        OperationalManifest manifest, string branch, string? publicToken) => new(x.Id, x.RowVersion,
        manifest.Id, manifest.ManifestNumber, manifest.ShipmentId, x.BranchId, branch, x.Provider,
        x.ShippingCompanyName, x.ExternalReference, x.ReferenceType, x.Status, x.ProviderStatus, x.LastKnownPlace,
        x.EstimatedAt, x.LastSuccessfulSyncAt, x.NextSyncAt, publicToken, x.CreatedAt, x.LastModifiedAt);
    private static TrackingEventResponse MapEvent(ManifestTrackingEvent x) => new(x.Id, x.RowVersion,
        x.Source, x.ReviewState, x.InternalTitle, x.InternalDescription, x.ProviderStatus,
        x.PublicTitle, x.PublicDescription, x.Place, x.OccurredAt, x.EstimatedAt,
        x.ReviewedAt, x.PublishedAt);
    private Guid IdempotencyKey() => Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key)
        ? key : throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency key required",
            "Send a UUID Idempotency-Key header.");
    private long Version() => long.TryParse(Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"'), out var value)
        ? value : throw new ApiProblemException(428, "IF_MATCH_REQUIRED", "Version required",
            "Send the current row version in If-Match.");
    private static void CheckVersion(long current, long supplied, string entity)
    { if (current != supplied) throw new ApiProblemException(412, "PRECONDITION_FAILED", $"The {entity} changed",
        $"Reload the current {entity} before saving."); }
    private static void ValidateEvent(string title, string description, DateTime occurredAt, string reason)
    { if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) ||
          string.IsNullOrWhiteSpace(reason) || occurredAt == default || occurredAt.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        throw Rule("TRACKING_EVENT_INVALID", "Title, description, occurrence time and reason are required."); }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeReference(string provider, string reference,
        CarrierTrackingReferenceType referenceType)
    {
        var value = reference.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        if (value.Length is < 4 or > 100 ||
            value.Any(x => !char.IsLetterOrDigit(x) && x is not '-' and not '/'))
            throw Rule("TRACKING_REFERENCE_INVALID",
                "Tracking references must contain 4 to 100 letters, numbers, dashes or slashes.");
        if (provider == "MSC" && referenceType == CarrierTrackingReferenceType.ContainerNumber &&
            (value.Length != 11 || value[..4].Any(x => !char.IsLetter(x)) ||
             value[4..].Any(x => !char.IsDigit(x))))
            throw Rule("MSC_CONTAINER_NUMBER_INVALID",
                "MSC container numbers use four letters followed by seven digits.");
        return value;
    }
    private static string Quote(long value) => $"\"{value}\"";
    private static ApiProblemException Rule(string code, string detail) =>
        new(422, code, "Tracking rule was not satisfied", detail);
}

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("ProviderWebhook")]
[Route("api/integrations/tracking/webhooks/{provider}")]
public sealed class ManifestTrackingWebhooksController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    public ManifestTrackingWebhooksController(ApplicationDbContext context, IConfiguration configuration)
    { _context = context; _configuration = configuration; }

    [HttpPost]
    public async Task<ActionResult> Receive(string provider, TrackingWebhookRequest request,
        [FromHeader(Name = "X-Carrier-Webhook-Secret")] string? suppliedSecret,
        CancellationToken cancellationToken)
    {
        var expected = _configuration["CarrierWebhooks:SharedSecret"];
        if (string.IsNullOrWhiteSpace(expected)) return StatusCode(503);
        if (!Match(expected, suppliedSecret)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.ProviderEventId) || string.IsNullOrWhiteSpace(request.ExternalReference))
            return BadRequest();
        var normalizedProvider = provider.Trim().ToUpperInvariant();
        var reference = request.ExternalReference.Trim().ToUpperInvariant();
        var connection = await _context.ManifestTrackingConnections.FirstOrDefaultAsync(x =>
            x.Provider == normalizedProvider && x.ExternalReference == reference, cancellationToken);
        if (connection is null) return NotFound();
        var existing = await _context.ManifestTrackingEvents.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ConnectionId == connection.Id && x.ProviderEventId == request.ProviderEventId, cancellationToken);
        if (existing is not null) return Ok(new { existing.Id, duplicate = true });
        var value = new ManifestTrackingEvent { BranchId = connection.BranchId, ConnectionId = connection.Id,
            ProviderEventId = request.ProviderEventId.Trim(), Source = TrackingEventSource.Provider,
            ReviewState = TrackingEventReviewState.Draft, ProviderStatus = request.Status.Trim(),
            InternalTitle = request.Title.Trim(), InternalDescription = request.Description.Trim(),
            Place = string.IsNullOrWhiteSpace(request.Place) ? null : request.Place.Trim(),
            OccurredAt = request.OccurredAt.ToUniversalTime(), EstimatedAt = request.EstimatedAt?.ToUniversalTime(),
            RawPayloadProtected = JsonSerializer.Serialize(request) };
        connection.ProviderStatus = value.ProviderStatus; connection.LastSuccessfulSyncAt = DateTime.UtcNow;
        connection.NextSyncAt = DateTime.UtcNow.AddHours(6); connection.LastKnownPlace = value.Place;
        connection.EstimatedAt = value.EstimatedAt;
        connection.Status = request.IsException ? ManifestTrackingStatus.Delayed : Status(request.Status);
        connection.RowVersion++; _context.ManifestTrackingEvents.Add(value);
        await _context.SaveChangesAsync(cancellationToken);
        return Accepted(new { value.Id, duplicate = false, reviewRequired = true });
    }

    private static ManifestTrackingStatus Status(string value) => value.Replace("_", "").Replace("-", "")
        .ToLowerInvariant() switch { "delayed" => ManifestTrackingStatus.Delayed,
            "arrived" => ManifestTrackingStatus.Arrived, "delivered" => ManifestTrackingStatus.Delivered,
            _ => ManifestTrackingStatus.InTransit };
    private static bool Match(string expected, string? supplied)
    {
        if (string.IsNullOrEmpty(supplied)) return false;
        var a = Encoding.UTF8.GetBytes(expected); var b = Encoding.UTF8.GetBytes(supplied);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("PublicRead")]
[Route("api/public/tracking")]
public sealed class PublicManifestTrackingController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public PublicManifestTrackingController(ApplicationDbContext context) => _context = context;

    [HttpGet("{publicToken}")]
    public async Task<ActionResult<PublicManifestTrackingResponse>> Get(string publicToken,
        CancellationToken cancellationToken)
    {
        var token = publicToken.Trim().ToLowerInvariant();
        if (token.Length is < 32 or > 128) return NotFound();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var row = await (from connection in _context.ManifestTrackingConnections.AsNoTracking()
                         join manifest in _context.OperationalManifests.AsNoTracking()
                             on connection.ManifestId equals manifest.Id
                         where connection.PublicTokenHash == hash
                         select new { Connection = connection, Manifest = manifest })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound();
        return Ok(await PublicResponseAsync(row.Connection.Id, row.Manifest,
            row.Manifest.ManifestNumber, cancellationToken));
    }

    /// <summary>
    /// Customer-facing tracking by the immutable BiGSAM shipment invoice number.
    /// MSC container and bill-of-lading references are never returned.
    /// </summary>
    [HttpGet("invoice/{invoiceNumber:regex(^BGL\\d+INV$)}")]
    public async Task<ActionResult<PublicManifestTrackingResponse>> GetByInvoice(
        string invoiceNumber, CancellationToken cancellationToken)
    {
        var number = invoiceNumber.Trim().ToUpperInvariant();
        var row = await (from shipment in _context.CustomerShipments.AsNoTracking()
                         join line in _context.OperationalManifestEntries.AsNoTracking()
                             on shipment.Id equals line.CustomerShipmentId
                         join manifest in _context.OperationalManifests.AsNoTracking()
                             on line.ManifestId equals manifest.Id
                         join connection in _context.ManifestTrackingConnections.AsNoTracking()
                             on manifest.Id equals connection.ManifestId
                         where shipment.InvoiceNumber == number
                         select new { Connection = connection, Manifest = manifest })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound();
        return Ok(await PublicResponseAsync(row.Connection.Id, row.Manifest, number,
            cancellationToken));
    }

    private async Task<PublicManifestTrackingResponse> PublicResponseAsync(Guid connectionId,
        OperationalManifest manifest, string trackingNumber, CancellationToken cancellationToken)
    {
        var events = await _context.ManifestTrackingEvents.AsNoTracking().Where(x =>
                x.ConnectionId == connectionId &&
                x.ReviewState == TrackingEventReviewState.Published)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(100)
            .Select(x => new PublicManifestTrackingEventResponse(
                x.PublicTitle!, x.PublicDescription!, x.Place, x.OccurredAt, x.EstimatedAt))
            .ToListAsync(cancellationToken);
        var latest = events.FirstOrDefault();
        var method = manifest.LogisticsMethod == ManifestLogisticsMethod.Air ? "Air" : "Sea";
        return new PublicManifestTrackingResponse(trackingNumber, manifest.ShipmentId, method,
            manifest.OriginCity, manifest.DestinationCity, $"BiGSAM {method} Shipping",
            PublicStatus(latest?.Title), latest?.Place,
            events.Select(x => x.EstimatedAt).FirstOrDefault(x => x.HasValue), events);
    }

    private static string PublicStatus(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Awaiting update";
        var value = title.ToLowerInvariant();
        if (value.Contains("deliver") || value.Contains("collect")) return "Delivered";
        if (value.Contains("delay") || value.Contains("hold") || value.Contains("issue"))
            return "On hold";
        if (value.Contains("book") || value.Contains("ready")) return "Getting ready";
        return "In transit";
    }
}
