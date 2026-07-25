using System.Security.Cryptography;
using System.Text;
using Api.Exceptions;
using Api.Services;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

/// <summary>MSC provider lookup and synchronization. Provider events remain internal drafts.</summary>
[ApiController]
[Authorize(Policy = "OperationsTeam")]
[Route("api/tracking/providers/msc")]
public sealed class MscTrackingProviderController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    private readonly IMscTrackingGateway _msc;
    private readonly IManifestTrackingIngestionService _ingestion;

    public MscTrackingProviderController(ApplicationDbContext context,
        IActiveBranchContext branches, IMscTrackingGateway msc,
        IManifestTrackingIngestionService ingestion)
    {
        _context = context;
        _branches = branches;
        _msc = msc;
        _ingestion = ingestion;
    }

    [HttpPost("lookup")]
    [EnableRateLimiting("CarrierSync")]
    public async Task<ActionResult> Lookup(MscTrackingLookupRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var manifest = await _context.OperationalManifests.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == request.ManifestId && (branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken) ?? throw new KeyNotFoundException("Manifest not found.");
        EnsureSea(manifest);
        var reference = Reference(request.TrackingNumber, request.TrackingType);
        var snapshot = await _msc.FetchAsync(reference, request.TrackingType, cancellationToken);
        return Ok(new
        {
            request.ManifestId,
            TrackingType = request.TrackingType,
            TrackingNumber = reference,
            ContainerNumber = request.TrackingType == CarrierTrackingReferenceType.ContainerNumber
                ? snapshot.ContainerNumber : null,
            BillOfLadingNumber = request.TrackingType == CarrierTrackingReferenceType.BillOfLading
                ? reference : null,
            InternalProviderStatus = snapshot.Status,
            CurrentLocation = snapshot.CurrentLocation,
            EstimatedArrival = snapshot.EstimatedArrivalAt,
            snapshot.IsMock,
            InternalProviderEvents = snapshot.Events.Select(x => new
            {
                x.ExternalEventId, x.EventClassifier, x.EventTypeCode, x.EventStatus,
                x.Description, x.Location, x.OccurredAt, x.EstimatedArrivalAt
            })
        });
    }

    [HttpPost("sync")]
    [EnableRateLimiting("CarrierSync")]
    public async Task<ActionResult> Sync(MscTrackingSyncRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var key = RequireIdempotencyKey();
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("SYNC_REASON_REQUIRED", "An MSC synchronization reason is required.");
        var manifest = await _context.OperationalManifests.FirstOrDefaultAsync(x =>
            x.Id == request.ManifestId && x.BranchId == branch.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException("Manifest not found.");
        EnsureSea(manifest);
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "msc.manifest.sync" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null)
            return Ok(await Current(request.ManifestId, true, cancellationToken));

        var reference = Reference(request.TrackingNumber, request.TrackingType);
        var connection = await _context.ManifestTrackingConnections.FirstOrDefaultAsync(x =>
            x.ManifestId == manifest.Id, cancellationToken);
        string? publicToken = null;
        if (connection is null)
        {
            publicToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            connection = new ManifestTrackingConnection
            {
                BranchId = branch.BranchId!.Value,
                ManifestId = manifest.Id,
                PublicTokenHash = Hash(publicToken)
            };
            _context.ManifestTrackingConnections.Add(connection);
        }
        else
        {
            CheckVersion(connection.RowVersion, RequireVersion());
            connection.RowVersion++;
        }
        connection.Provider = "MSC";
        connection.ShippingCompanyName = "MSC";
        connection.ExternalReference = reference;
        connection.ReferenceType = request.TrackingType;
        connection.Status = ManifestTrackingStatus.Connected;
        connection.NextSyncAt = DateTime.UtcNow;
        manifest.CarrierName = "MSC";
        manifest.CarrierReference = reference;
        manifest.TrackingProvider = "MSC";
        if (request.TrackingType == CarrierTrackingReferenceType.ContainerNumber)
            manifest.ContainerOrFlightNumber = reference;
        else
            manifest.BillOfLadingNumber = reference;
        manifest.LastEditedByUserId = CurrentUserId;
        manifest.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);

        var result = await _ingestion.SyncAsync(connection.Id, cancellationToken);
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            ActorUserId = CurrentUserId, Operation = "msc.manifest.sync", Key = key,
            ResourceType = nameof(ManifestTrackingConnection), ResourceId = connection.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _context.SaveChangesAsync(cancellationToken);
        var current = await Current(request.ManifestId, false, cancellationToken);
        return Ok(new
        {
            Connection = current,
            PublicTrackingToken = publicToken,
            result.EventsReceived, result.NewEventsStored, result.DuplicatesIgnored,
            result.IsMock, result.SyncedAt,
            CustomerPublicationRequired = true,
            Message = "MSC movements were imported as internal drafts. Review the wording before showing an update to clients."
        });
    }

    private async Task<object> Current(Guid manifestId, bool replayed,
        CancellationToken cancellationToken)
    {
        var row = await _context.ManifestTrackingConnections.AsNoTracking().SingleAsync(x =>
            x.ManifestId == manifestId, cancellationToken);
        var drafts = await _context.ManifestTrackingEvents.AsNoTracking().CountAsync(x =>
            x.ConnectionId == row.Id && x.ReviewState == TrackingEventReviewState.Draft,
            cancellationToken);
        return new
        {
            row.Id, row.RowVersion, row.ManifestId, row.Provider, row.ShippingCompanyName,
            row.ExternalReference, row.ReferenceType, row.Status,
            InternalProviderStatus = row.ProviderStatus,
            row.LastKnownPlace, row.EstimatedAt, row.LastSuccessfulSyncAt, row.NextSyncAt,
            InternalDraftEvents = drafts, Replayed = replayed
        };
    }

    private static void EnsureSea(OperationalManifest manifest)
    {
        if (manifest.LogisticsMethod != ManifestLogisticsMethod.Ship)
            throw Rule("MSC_REQUIRES_SEA_MANIFEST",
                "MSC container or bill-of-lading tracking can only be connected to a Sea manifest.");
    }

    private static string Reference(string value, CarrierTrackingReferenceType type)
    {
        var reference = value.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        if (reference.Length is < 4 or > 100 ||
            reference.Any(x => !char.IsLetterOrDigit(x) && x is not '-' and not '/'))
            throw Rule("MSC_REFERENCE_INVALID",
                "Use a valid MSC container or bill-of-lading reference.");
        if (type == CarrierTrackingReferenceType.ContainerNumber &&
            (reference.Length != 11 || reference[..4].Any(x => !char.IsLetter(x)) ||
             reference[4..].Any(x => !char.IsDigit(x))))
            throw Rule("MSC_CONTAINER_NUMBER_INVALID",
                "MSC container numbers use four letters followed by seven digits.");
        return reference;
    }

    private Guid RequireIdempotencyKey() =>
        Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key) ? key :
            throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency key required",
                "Send a UUID Idempotency-Key header.");
    private long RequireVersion()
    {
        var raw = Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        return long.TryParse(raw, out var version) ? version :
            throw new ApiProblemException(428, "IF_MATCH_REQUIRED", "Version required",
                "Send the current tracking connection row version in If-Match.");
    }
    private static void CheckVersion(long current, long supplied)
    {
        if (current != supplied)
            throw new ApiProblemException(412, "VERSION_MISMATCH", "The tracking connection changed",
                "Reload the current connection before synchronizing it again.");
    }
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static ApiProblemException Rule(string code, string detail) =>
        new(422, code, "MSC tracking rule was not satisfied", detail);
}
