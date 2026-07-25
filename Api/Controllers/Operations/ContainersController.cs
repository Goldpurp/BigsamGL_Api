using System.Text.Json;
using System.Text.RegularExpressions;
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
[Route("api/operations/containers")]
public sealed partial class ContainersController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IContainerTrackingService _tracking;
    private readonly ICargoAllocationService _allocations;
    public ContainersController(IApplicationDbContext context, IContainerTrackingService tracking,
        ICargoAllocationService allocations)
    {
        _context = context; _tracking = tracking; _allocations = allocations;
    }

    private Guid CurrentUserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id)
        ? id : throw new UnauthorizedAccessException("User identity is missing.");

    [HttpPost]
    public async Task<ActionResult<ShipmentContainer>> Create(CreateShipmentContainerRequest request,
        CancellationToken cancellationToken)
    {
        var number = request.ContainerNumber.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        var provider = request.Provider.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(provider)) return BadRequest("Container provider is required.");
        if (provider == "MSC" && !ContainerNumberPattern().IsMatch(number))
            return BadRequest("MSC container number must use the ISO 6346 format: four letters and seven digits.");
        if (number.Length is < 4 or > 20) return BadRequest("Container number must contain 4 to 20 characters.");
        if (!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Trim().Length > 120)
            return BadRequest("Container name cannot exceed 120 characters.");
        if (request.ContainerType == ContainerType.NotSpecified)
            return BadRequest("Select the physical container type.");
        if (await _context.ShipmentContainers.AnyAsync(x => x.Provider == provider && x.ContainerNumber == number,
                cancellationToken))
            return Conflict("This provider container is already registered.");
        if (request.ShippingBatchId.HasValue &&
            !await _context.ShippingBatches.AnyAsync(x => x.Id == request.ShippingBatchId, cancellationToken))
            return BadRequest("Shipment batch not found.");
        if (request.ShipmentMovementId.HasValue)
        {
            var movement = await _context.ShipmentMovements.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == request.ShipmentMovementId, cancellationToken);
            if (movement is null) return BadRequest("Shipment movement not found.");
            if (movement.Mode != TransportMode.Sea) return BadRequest("Containers can only belong to sea movements.");
        }
        if (request.LinkAllBatchCargo)
            return BadRequest("Use the shipment allocation endpoint after selecting each cargo invoice.");
        var container = new ShipmentContainer
        {
            Provider = provider, ContainerNumber = number, Type = request.ContainerType,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            ShippingBatchId = request.ShippingBatchId, ShipmentMovementId = request.ShipmentMovementId,
            CarrierBookingReference = request.CarrierBookingReference?.Trim(),
            BillOfLadingNumber = request.BillOfLadingNumber?.Trim()
        };
        _context.ShipmentContainers.Add(container);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/containers/{container.Id}", container);
    }

    /// <summary>
    /// Corrects the official container identity or edits its friendly dashboard name and placement.
    /// Every change is retained in the audit log.
    /// </summary>
    [HttpPut("{containerId:guid}")]
    public async Task<ActionResult<ShipmentContainer>> Update(Guid containerId,
        UpdateShipmentContainerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ContainerNumber) || string.IsNullOrWhiteSpace(request.Provider))
            return BadRequest("Container number and provider are required.");
        var number = request.ContainerNumber.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        var provider = request.Provider.Trim().ToUpperInvariant();
        if (provider == "MSC" && !ContainerNumberPattern().IsMatch(number))
            return BadRequest("MSC container number must use the ISO 6346 format: four letters and seven digits.");
        if (number.Length is < 4 or > 20) return BadRequest("Container number must contain 4 to 20 characters.");
        if (request.ContainerType == ContainerType.NotSpecified)
            return BadRequest("Select the physical container type.");
        if (!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Trim().Length > 120)
            return BadRequest("Container name cannot exceed 120 characters.");
        if (await _context.ShipmentContainers.AsNoTracking().AnyAsync(x => x.Id != containerId &&
                x.Provider == provider && x.ContainerNumber == number, cancellationToken))
            return Conflict("This provider container is already registered.");
        ShipmentMovement? movement = null;
        if (request.ShipmentMovementId.HasValue)
        {
            movement = await _context.ShipmentMovements.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == request.ShipmentMovementId, cancellationToken);
            if (movement is null) return BadRequest("Shipment movement not found.");
            if (movement.Mode != TransportMode.Sea) return BadRequest("Containers can only belong to sea movements.");
        }
        if (request.ShippingBatchId.HasValue &&
            !await _context.ShippingBatches.AsNoTracking().AnyAsync(x => x.Id == request.ShippingBatchId,
                cancellationToken)) return BadRequest("Shipment batch not found.");
        if (movement?.ShippingBatchId is Guid movementBatchId && request.ShippingBatchId is Guid batchId &&
            movementBatchId != batchId) return BadRequest("Container batch must match its sea movement batch.");

        var container = await _context.ShipmentContainers.FirstOrDefaultAsync(x => x.Id == containerId,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment container not found.");
        var numberChanged = container.ContainerNumber != number;
        container.Provider = provider;
        container.ContainerNumber = number;
        container.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        container.Type = request.ContainerType;
        container.ShippingBatchId = request.ShippingBatchId;
        container.ShipmentMovementId = request.ShipmentMovementId;
        container.CarrierBookingReference = request.CarrierBookingReference?.Trim();
        container.BillOfLadingNumber = request.BillOfLadingNumber?.Trim();
        container.IsActive = request.IsActive;
        if (numberChanged)
        {
            var manifestLinks = await _context.OperationalManifestEntries.Where(x =>
                x.ShipmentContainerId == containerId).ToListAsync(cancellationToken);
            foreach (var link in manifestLinks) link.ContainerNumberSnapshot = number;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(container);
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] bool activeOnly = true,
        [FromQuery] Guid? shipmentMovementId = null, [FromQuery] ContainerType? containerType = null,
        [FromQuery] string? provider = null, [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] bool paged = false, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ShipmentContainers.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        if (shipmentMovementId.HasValue) query = query.Where(x => x.ShipmentMovementId == shipmentMovementId);
        if (containerType.HasValue) query = query.Where(x => x.Type == containerType);
        if (!string.IsNullOrWhiteSpace(provider))
        {
            var value = provider.Trim().ToUpperInvariant();
            query = query.Where(x => x.Provider == value);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var value = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status.ToLower() == value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.ContainerNumber.ToLower().Contains(value) ||
                (x.CarrierBookingReference != null && x.CarrierBookingReference.ToLower().Contains(value)) ||
                (x.BillOfLadingNumber != null && x.BillOfLadingNumber.ToLower().Contains(value)));
        }
        var ordered = query.OrderByDescending(x => x.LastEventAt ?? x.CreatedAt).ThenByDescending(x => x.Id);
        PagedResponse<ShipmentContainer>? pageResult = null;
        IReadOnlyList<ShipmentContainer> containers;
        if (paged)
        {
            pageResult = await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
            containers = pageResult.Items;
        }
        else containers = await ordered.Take(500).ToListAsync(cancellationToken);
        var ids = containers.Select(x => x.Id).ToList();
        var counts = await _context.CargoItems.AsNoTracking().Where(x => x.ShipmentContainerId.HasValue && ids.Contains(x.ShipmentContainerId.Value))
            .GroupBy(x => x.ShipmentContainerId!.Value).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var items = containers.Select(x => new { Container = x, CargoCount = counts.GetValueOrDefault(x.Id) }).ToList();
        return pageResult is null ? Ok(items) : Ok(new { items, pageResult.Page, pageResult.PageSize,
            pageResult.HasPrevious, pageResult.HasNext, pageResult.Total, pageResult.TotalPages });
    }

    [HttpGet("{containerId:guid}")]
    public async Task<ActionResult> Get(Guid containerId, CancellationToken cancellationToken)
    {
        var container = await _context.ShipmentContainers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken)
            ?? throw new KeyNotFoundException("Shipment container not found.");
        var cargo = await _context.CargoItems.AsNoTracking().Where(x => x.ShipmentContainerId == containerId)
            .Select(x => new { x.Id, x.TrackingNumber, x.MarkerCode, x.Description, x.Status, x.CustomerId }).ToListAsync(cancellationToken);
        var events = await _context.CarrierTrackingEvents.AsNoTracking().Where(x => x.ShipmentContainerId == containerId)
            .OrderByDescending(x => x.OccurredAt).Take(200).ToListAsync(cancellationToken);
        return Ok(new { container, cargo, events });
    }

    [HttpPost("{containerId:guid}/cargo")]
    public async Task<IActionResult> LinkCargo(Guid containerId, LinkContainerCargoRequest request,
        CancellationToken cancellationToken)
    {
        var container = await _context.ShipmentContainers.FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken)
            ?? throw new KeyNotFoundException("Shipment container not found.");
        var cargo = await _context.CargoItems.FirstOrDefaultAsync(x => x.Id == request.CargoId, cancellationToken)
            ?? throw new KeyNotFoundException("Cargo not found.");
        if (!container.ShipmentMovementId.HasValue)
            return Conflict("Assign the container to a sea movement before adding cargo.");
        if (!cargo.InvoiceId.HasValue)
            return Conflict("Link cargo to its invoice before assigning its container.");
        await _allocations.AllocateAsync(new AllocateCargoCommand(cargo.InvoiceId.Value,
            new[] { cargo.Id }, container.ShipmentMovementId.Value, container.Id,
            "Assigned through the container cargo endpoint."), CurrentUserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{containerId:guid}/sync")]
    public async Task<ActionResult<ContainerSyncResult>> Sync(Guid containerId, [FromQuery] bool notifyClients = true,
        CancellationToken cancellationToken = default) =>
        Ok(await _tracking.SyncAsync(containerId, notifyClients, cancellationToken));

    [HttpPost("{containerId:guid}/events")]
    public async Task<ActionResult<ContainerSyncResult>> AddEvent(Guid containerId,
        CreateContainerEventRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EventTypeCode) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("Event type and description are required.");
        var externalId = $"staff-{containerId:N}-{Guid.NewGuid():N}";
        var raw = JsonSerializer.Serialize(new { Manual = true, request.EventClassifier,
            request.EventTypeCode, request.Description, request.Location, request.OccurredAt, request.EstimatedArrivalAt });
        var trackingEvent = new MscTrackingEvent(externalId, request.EventClassifier.Trim().ToUpperInvariant(),
            request.EventTypeCode.Trim().ToUpperInvariant(), "NEW", request.Description.Trim(), request.Location.Trim(),
            request.OccurredAt, request.EstimatedArrivalAt, raw);
        return Ok(await _tracking.RecordManualEventAsync(containerId, trackingEvent,
            request.NotifyClients, cancellationToken));
    }

    [GeneratedRegex("^[A-Z]{4}[0-9]{7}$", RegexOptions.CultureInvariant)]
    private static partial Regex ContainerNumberPattern();
}
