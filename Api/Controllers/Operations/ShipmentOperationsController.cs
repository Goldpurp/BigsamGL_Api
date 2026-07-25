using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Authorize(Policy = "OperationsTeam")]
[Route("api/operations")]
public sealed class ShipmentOperationsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IIntegrationGateway _integrations;
    private readonly IShipmentTrackingService _tracking;
    public ShipmentOperationsController(IApplicationDbContext context, IIntegrationGateway integrations,
        IShipmentTrackingService tracking)
    {
        _context = context; _integrations = integrations; _tracking = tracking;
    }

    [HttpPost("cargo/{cargoId:guid}/milestones")]
    public async Task<ActionResult<ShipmentMilestone>> AddMilestone(Guid cargoId, CreateMilestoneRequest request, CancellationToken cancellationToken)
    {
        var milestone = await _tracking.RecordAsync(cargoId, ToInput(request, "BiGSAM Staff"), cancellationToken);
        return Created($"/api/operations/cargo/{cargoId}/milestones/{milestone.Id}", milestone);
    }

    [HttpPut("cargo/{cargoId:guid}/milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ShipmentMilestone>> CorrectMilestone(Guid cargoId, Guid milestoneId,
        CorrectMilestoneRequest request, CancellationToken cancellationToken)
    {
        var input = new ShipmentTrackingInput(request.Status, request.EventType, request.Title, request.Description,
            request.Location, request.OccurredAt, request.Visibility, request.EstimatedDeliveryAt,
            request.NotifyCustomer, request.IsException, request.IssueCode, "BiGSAM Staff Correction");
        return Ok(await _tracking.CorrectAsync(cargoId, milestoneId, input, request.RevisionReason, cancellationToken));
    }

    [HttpGet("cargo/{cargoId:guid}/milestones")]
    public async Task<ActionResult> GetMilestones(Guid cargoId, CancellationToken cancellationToken) => Ok(
        await _context.ShipmentMilestones.AsNoTracking().Where(x => x.CargoItemId == cargoId)
            .OrderByDescending(x => x.OccurredAt).ToListAsync(cancellationToken));

    [HttpPut("cargo/{cargoId:guid}/carrier")]
    public async Task<IActionResult> BindCarrier(Guid cargoId, BindCarrierRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.ExternalTrackingNumber))
            return BadRequest("Carrier provider and external tracking number are required.");
        var cargo = await _context.CargoItems.FirstOrDefaultAsync(x => x.Id == cargoId, cancellationToken)
            ?? throw new KeyNotFoundException("Cargo not found.");
        cargo.CarrierProvider = request.Provider.Trim().ToUpperInvariant();
        cargo.ExternalTrackingNumber = request.ExternalTrackingNumber.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("cargo/{cargoId:guid}/carrier-sync")]
    public async Task<ActionResult> SyncCarrier(Guid cargoId, [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        var cargo = await _context.CargoItems.FirstOrDefaultAsync(x => x.Id == cargoId, cancellationToken)
            ?? throw new KeyNotFoundException("Cargo not found.");
        var selectedProvider = provider ?? cargo.CarrierProvider ?? "GDH";
        var externalTracking = cargo.ExternalTrackingNumber ?? cargo.TrackingNumber;
        var update = await _integrations.GetCarrierUpdateAsync(selectedProvider, externalTracking, cancellationToken);
        var milestone = await _tracking.RecordAsync(cargo.Id, new ShipmentTrackingInput(update.MappedStatus,
            ShipmentEventType.CarrierUpdate, update.Title, update.Description, update.Location, update.OccurredAt,
            MilestoneVisibility.CustomerVisible, update.EstimatedDeliveryAt, true, false, null,
            $"{update.Provider} Carrier", update.Provider, update.ExternalEventId), cancellationToken);
        return Ok(new { update, milestone.Id });
    }

    [HttpPost("documents")]
    public async Task<ActionResult> RegisterDocument(RegisterDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!request.CargoItemId.HasValue && !request.CustomerId.HasValue)
            return BadRequest("Client or cargo item is required.");
        if (request.SizeBytes <= 0 || request.SizeBytes > 20_000_000)
            return BadRequest("Document size must be between 1 byte and 20 MB.");
        var stored = await _integrations.StoreDocumentAsync(request.FileName, request.ContentType, request.SizeBytes, cancellationToken);
        if (request.CustomerId.HasValue && !await _context.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken))
            return BadRequest("Client not found.");
        var document = new ShipmentDocument { CustomerId = request.CustomerId, CargoItemId = request.CargoItemId,
            Type = request.Type, FileName = request.FileName.Trim(),
            ContentType = request.ContentType.Trim(), SizeBytes = request.SizeBytes, StorageKey = stored.StorageKey, Checksum = stored.Checksum };
        _context.ShipmentDocuments.Add(document); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/documents/{document.Id}", new { document, stored.DownloadUrl });
    }

    private static ShipmentTrackingInput ToInput(CreateMilestoneRequest request, string source) => new(
        request.Status, request.EventType, request.Title, request.Description, request.Location,
        request.OccurredAt, request.Visibility, request.EstimatedDeliveryAt, request.NotifyCustomer,
        request.IsException, request.IssueCode, source);
}
