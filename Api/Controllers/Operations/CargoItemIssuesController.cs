using Api.Exceptions;
using Api.Services;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Authorize(Policy = "OperationsTeam")]
[Route("api/operations/item-issues")]
public sealed class CargoItemIssuesController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    private readonly ICompanyDocumentNumberGenerator _numbers;
    private readonly IClientCommunicationService _communications;

    public CargoItemIssuesController(ApplicationDbContext context, IActiveBranchContext branches,
        ICompanyDocumentNumberGenerator numbers, IClientCommunicationService communications)
    { _context = context; _branches = branches; _numbers = numbers; _communications = communications; }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search, [FromQuery] CargoItemIssueType? type,
        [FromQuery] CargoItemIssueSeverity? severity, [FromQuery] CargoItemIssueState? state,
        [FromQuery] string? invoiceNumber, [FromQuery] Guid? manifestId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.CargoItemIssues.AsNoTracking().Where(x =>
            branch.IsAllBranches || x.BranchId == branch.BranchId);
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (severity.HasValue) query = query.Where(x => x.Severity == severity);
        if (state.HasValue) query = query.Where(x => x.State == state);
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        { var value = invoiceNumber.Trim().ToUpperInvariant(); query = query.Where(x => x.InvoiceNumber == value); }
        if (manifestId.HasValue) query = query.Where(x => x.ManifestId == manifestId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.IssueNumber.ToLower().Contains(value) ||
                (x.InvoiceNumber != null && x.InvoiceNumber.ToLower().Contains(value)) ||
                x.ExpectedDetails.ToLower().Contains(value) || x.FoundDetails.ToLower().Contains(value) ||
                (x.LastSeenPlace != null && x.LastSeenPlace.ToLower().Contains(value)));
        }
        var result = await query.OrderByDescending(x => x.Severity).ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id).ToPagedResponseAsync(page, pageSize, true, cancellationToken);
        var evidence = await _context.CargoItemIssueEvidence.AsNoTracking().Where(x =>
                result.Items.Select(y => y.Id).Contains(x.CargoItemIssueId))
            .GroupBy(x => x.CargoItemIssueId).Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var keys = await BranchKeysAsync(result.Items.Select(x => x.BranchId), cancellationToken);
        return Ok(new PagedResponse<CargoItemIssueResponse>(result.Items.Select(x => Map(x,
            keys[x.BranchId], evidence.GetValueOrDefault(x.Id))).ToList(), result.Page, result.PageSize,
            result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
    }

    [HttpPost]
    public async Task<ActionResult<CargoItemIssueResponse>> Create(CreateCargoItemIssueRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var key = IdempotencyKey();
        Validate(request);
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "cargo-issue.create" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null)
            return Ok(Map(await ScopedAsync(prior.ResourceId, branch.BranchId!.Value, false, cancellationToken),
                branch.BranchKey!, await EvidenceCountAsync(prior.ResourceId, cancellationToken)));
        var linked = await ResolveLinksAsync(request.InvoiceNumber, request.ManifestId,
            request.ManifestLineId, branch.BranchId!.Value, cancellationToken);
        var issue = new CargoItemIssue { BranchId = branch.BranchId.Value,
            IssueNumber = await _numbers.NextCargoItemIssueAsync(cancellationToken), Type = request.Type,
            Severity = request.Severity, CustomerShipmentId = linked.ShipmentId,
            InvoiceNumber = linked.InvoiceNumber, ManifestId = linked.ManifestId,
            ManifestLineId = linked.LineId, ExpectedDetails = request.ExpectedDetails.Trim(),
            FoundDetails = request.FoundDetails.Trim(), LastSeenPlace = Null(request.LastSeenPlace),
            LastSeenAt = request.LastSeenAt?.ToUniversalTime(), InternalNote = request.InternalNote.Trim(),
            CustomerVisible = request.CustomerVisible,
            State = request.Severity == CargoItemIssueSeverity.Critical
                ? CargoItemIssueState.OnHold : CargoItemIssueState.Open };
        _context.CargoItemIssues.Add(issue);
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "cargo-issue.create", Key = key, ResourceType = nameof(CargoItemIssue),
            ResourceId = issue.Id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken); Response.Headers.ETag = Quote(issue.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = issue.Id }, Map(issue, branch.BranchKey!, 0));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var issue = await _context.CargoItemIssues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken)
            ?? throw new KeyNotFoundException("Cargo item issue not found.");
        var key = (await BranchKeysAsync([issue.BranchId], cancellationToken))[issue.BranchId];
        var evidence = await _context.CargoItemIssueEvidence.AsNoTracking().Where(x => x.CargoItemIssueId == id)
            .OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.ShipmentDocumentId, x.FileName,
                x.ContentType, x.SizeBytes, x.Sha256, x.CreatedAt }).ToListAsync(cancellationToken);
        Response.Headers.ETag = Quote(issue.RowVersion);
        return Ok(new { Issue = Map(issue, key, evidence.Count), Evidence = evidence });
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<CargoItemIssueResponse>> Update(Guid id,
        UpdateCargoItemIssueRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var version = Version();
        if (string.IsNullOrWhiteSpace(request.ExpectedDetails) || string.IsNullOrWhiteSpace(request.FoundDetails) ||
            string.IsNullOrWhiteSpace(request.InternalNote) || string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("ISSUE_DETAILS_REQUIRED", "Expected/found details, internal note and change reason are required.");
        var issue = await ScopedAsync(id, branch.BranchId!.Value, true, cancellationToken);
        CheckVersion(issue.RowVersion, version);
        if (issue.State == CargoItemIssueState.Resolved)
            throw Rule("ISSUE_RESOLVED", "A resolved issue cannot be rewritten.");
        if (request.State == CargoItemIssueState.Resolved)
            throw Rule("USE_RESOLVE_ACTION", "Use the controlled resolve action to close an issue.");
        issue.Type = request.Type; issue.Severity = request.Severity; issue.State = request.State;
        issue.ExpectedDetails = request.ExpectedDetails.Trim(); issue.FoundDetails = request.FoundDetails.Trim();
        issue.LastSeenPlace = Null(request.LastSeenPlace); issue.LastSeenAt = request.LastSeenAt?.ToUniversalTime();
        issue.InternalNote = $"{request.InternalNote.Trim()}\nEdit reason: {request.Reason.Trim()}";
        issue.CustomerVisible = request.CustomerVisible; issue.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken); Response.Headers.ETag = Quote(issue.RowVersion);
        return Ok(Map(issue, branch.BranchKey!, await EvidenceCountAsync(id, cancellationToken)));
    }

    [HttpPost("{id:guid}/evidence")]
    public async Task<ActionResult> Evidence(Guid id, AddCargoIssueEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var key = IdempotencyKey();
        var issue = await ScopedAsync(id, branch.BranchId!.Value, true, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.ContentType) ||
            request.SizeBytes <= 0 || request.SizeBytes > 25_000_000 || request.Sha256.Length != 64)
            throw Rule("EVIDENCE_METADATA_INVALID", "Evidence requires safe file metadata, SHA-256 and a size up to 25 MB.");
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "cargo-issue.evidence" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null) return Ok(await _context.CargoItemIssueEvidence.AsNoTracking()
            .SingleAsync(x => x.Id == prior.ResourceId && x.BranchId == branch.BranchId, cancellationToken));
        if (request.ShipmentDocumentId.HasValue && !await _context.ShipmentDocuments.AsNoTracking().AnyAsync(x =>
            x.Id == request.ShipmentDocumentId, cancellationToken)) throw new KeyNotFoundException("Document not found.");
        var value = new CargoItemIssueEvidence { BranchId = branch.BranchId.Value,
            CargoItemIssueId = id, ShipmentDocumentId = request.ShipmentDocumentId,
            FileName = request.FileName.Trim(), ContentType = request.ContentType.Trim().ToLowerInvariant(),
            SizeBytes = request.SizeBytes, Sha256 = request.Sha256.Trim().ToUpperInvariant() };
        issue.RowVersion++; _context.CargoItemIssueEvidence.Add(value);
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "cargo-issue.evidence", Key = key, ResourceType = nameof(CargoItemIssueEvidence),
            ResourceId = value.Id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/item-issues/{id}", value);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<CargoItemIssueResponse>> Resolve(Guid id,
        ResolveCargoItemIssueRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var version = Version();
        if (!request.Confirmation.Equals("RESOLVE", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.Resolution))
            throw Rule("RESOLUTION_CONFIRMATION_REQUIRED", "Type RESOLVE and provide the resolution.");
        var issue = await ScopedAsync(id, branch.BranchId!.Value, true, cancellationToken);
        CheckVersion(issue.RowVersion, version);
        if (issue.State == CargoItemIssueState.Resolved) return Ok(Map(issue, branch.BranchKey!,
            await EvidenceCountAsync(id, cancellationToken)));
        issue.State = CargoItemIssueState.Resolved; issue.Resolution = request.Resolution.Trim();
        issue.ResolvedAt = DateTime.UtcNow; issue.ResolvedByUserId = CurrentUserId; issue.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(Map(issue, branch.BranchKey!, await EvidenceCountAsync(id, cancellationToken)));
    }

    [HttpPost("{id:guid}/notify-client")]
    public async Task<ActionResult> Notify(Guid id, NotifyCargoIssueClientRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var key = IdempotencyKey();
        if (!request.Confirmation.Equals("NOTIFY", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            throw Rule("NOTIFY_CONFIRMATION_REQUIRED", "Type NOTIFY and provide subject and body.");
        var issue = await ScopedAsync(id, branch.BranchId!.Value, true, cancellationToken);
        if (!issue.CustomerVisible) throw Rule("ISSUE_NOT_CUSTOMER_VISIBLE",
            "Mark the issue customer-visible and save it before the explicit notification action.");
        if (!issue.CustomerShipmentId.HasValue) throw Rule("CLIENT_LINK_REQUIRED",
            "Link an invoice shipment before notifying a client.");
        var prior = await _context.IdempotencyRecords.AsNoTracking().AnyAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "cargo-issue.notify" && x.Key == key &&
            x.ResourceId == id && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior) return Accepted(new { issueId = id, replayed = true, deliveryQueued = true });
        var customerId = await _context.CustomerShipments.AsNoTracking().Where(x =>
            x.Id == issue.CustomerShipmentId && x.BranchId == branch.BranchId).Select(x => x.CustomerId)
            .SingleAsync(cancellationToken);
        await _communications.SendAsync(customerId, CommunicationType.General, request.Subject.Trim(),
            request.Body.Trim(), nameof(CargoItemIssue), issue.Id, [], new { issue.IssueNumber,
                issue.InvoiceNumber, issue.Type, issue.Severity, issue.State }, cancellationToken);
        issue.ClientNotifiedAt = DateTime.UtcNow; issue.RowVersion++;
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "cargo-issue.notify", Key = key, ResourceType = nameof(CargoItemIssue),
            ResourceId = id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken);
        return Accepted(new { issueId = id, recipients = 1, deliveryQueued = true });
    }

    private async Task<(Guid? ShipmentId, string? InvoiceNumber, Guid? ManifestId, Guid? LineId)> ResolveLinksAsync(
        string? invoiceNumber, Guid? manifestId, Guid? lineId, Guid branchId, CancellationToken cancellationToken)
    {
        CustomerShipment? shipment = null; OperationalManifestEntry? line = null;
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
            shipment = await _context.CustomerShipments.AsNoTracking().FirstOrDefaultAsync(x =>
                x.InvoiceNumber == invoiceNumber.Trim().ToUpperInvariant() && x.BranchId == branchId,
                cancellationToken) ?? throw new KeyNotFoundException("Customer shipment invoice not found.");
        if (manifestId.HasValue && !await _context.OperationalManifests.AsNoTracking().AnyAsync(x =>
            x.Id == manifestId && x.BranchId == branchId, cancellationToken))
            throw new KeyNotFoundException("Manifest not found.");
        if (lineId.HasValue)
        {
            line = await (from value in _context.OperationalManifestEntries.AsNoTracking()
                          join manifest in _context.OperationalManifests.AsNoTracking()
                              on value.ManifestId equals manifest.Id
                          where value.Id == lineId && manifest.BranchId == branchId
                          select value).SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Manifest line not found.");
            if (manifestId.HasValue && line.ManifestId != manifestId) throw Rule("ISSUE_LINK_MISMATCH",
                "The manifest line does not belong to the selected manifest.");
            if (shipment is not null && line.CustomerShipmentId != shipment.Id) throw Rule("ISSUE_LINK_MISMATCH",
                "The invoice and manifest line refer to different customer shipments.");
            shipment ??= await _context.CustomerShipments.AsNoTracking().SingleAsync(x =>
                x.Id == line.CustomerShipmentId && x.BranchId == branchId, cancellationToken);
            manifestId ??= line.ManifestId;
        }
        if (shipment is null && !manifestId.HasValue)
            throw Rule("ISSUE_LINK_REQUIRED", "Link an invoice, manifest or manifest line to the issue.");
        return (shipment?.Id, shipment?.InvoiceNumber, manifestId, line?.Id);
    }

    private static void Validate(CreateCargoItemIssueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedDetails) || string.IsNullOrWhiteSpace(request.FoundDetails) ||
            string.IsNullOrWhiteSpace(request.InternalNote)) throw Rule("ISSUE_DETAILS_REQUIRED",
            "Expected details, found details and an internal note are required.");
        if (request.LastSeenAt?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            throw Rule("LAST_SEEN_IN_FUTURE", "Last-seen time cannot be in the future.");
    }

    private async Task<CargoItemIssue> ScopedAsync(Guid id, Guid branchId, bool tracked,
        CancellationToken cancellationToken)
    {
        var query = tracked ? _context.CargoItemIssues.AsQueryable() : _context.CargoItemIssues.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id && x.BranchId == branchId, cancellationToken)
            ?? throw new KeyNotFoundException("Cargo item issue not found.");
    }
    private Task<int> EvidenceCountAsync(Guid id, CancellationToken cancellationToken) =>
        _context.CargoItemIssueEvidence.AsNoTracking().CountAsync(x => x.CargoItemIssueId == id, cancellationToken);
    private async Task<Dictionary<Guid, string>> BranchKeysAsync(IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var values = ids.Distinct().ToList();
        return (await _context.Branches.AsNoTracking().Where(x => values.Contains(x.Id))
            .Select(x => new { x.Id, x.Code }).ToListAsync(cancellationToken)).ToDictionary(x => x.Id,
            x => x.Code switch { "UK-MAN" => "manchester", "NG-LOS" => "lagos",
                "CN-GZ" => "guangzhou", _ => x.Code.ToLowerInvariant() });
    }
    private static CargoItemIssueResponse Map(CargoItemIssue x, string branch, int evidence) =>
        new(x.Id, x.IssueNumber, x.RowVersion, x.BranchId, branch, x.Type, x.Severity, x.State,
            x.CustomerShipmentId, x.InvoiceNumber, x.ManifestId, x.ManifestLineId, x.ExpectedDetails,
            x.FoundDetails, x.LastSeenPlace, x.LastSeenAt, x.InternalNote, x.CustomerVisible, evidence,
            x.ResolvedAt, x.Resolution, x.ClientNotifiedAt, x.CreatedAt, x.LastModifiedAt);
    private Guid IdempotencyKey() => Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key)
        ? key : throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency key required",
            "Send a UUID Idempotency-Key header.");
    private long Version() => long.TryParse(Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"'), out var value)
        ? value : throw new ApiProblemException(428, "IF_MATCH_REQUIRED", "Version required",
            "Send the current row version in If-Match.");
    private static void CheckVersion(long current, long supplied)
    { if (current != supplied) throw new ApiProblemException(412, "PRECONDITION_FAILED", "The issue changed",
        "Reload the current issue before saving."); }
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Quote(long value) => $"\"{value}\"";
    private static ApiProblemException Rule(string code, string detail) =>
        new(422, code, "Cargo item issue rule was not satisfied", detail);
}
