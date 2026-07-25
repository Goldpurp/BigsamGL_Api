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
[Route("api/operations/shipment-movements")]
public sealed class ShipmentMovementsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    public ShipmentMovementsController(IApplicationDbContext context) => _context = context;

    [HttpPost]
    public async Task<ActionResult<ShipmentMovement>> Create(CreateShipmentMovementRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(request.Mode, request.OperatorType, request.CarrierName, request.FlightNumber,
            request.ScheduledDepartureAt, request.EstimatedArrivalAt);
        if (error is not null) return BadRequest(error);
        var code = request.MovementCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return BadRequest("Movement code is required.");
        if (await _context.ShipmentMovements.AnyAsync(x => x.MovementCode == code, cancellationToken))
            return Conflict("Shipment movement code is already in use.");
        var branches = await ResolveBranchesAsync(request.OriginBranchCode, request.DestinationBranchCode,
            cancellationToken);
        if (branches is null) return BadRequest("Active origin and destination branches are required.");
        if (request.ShippingBatchId.HasValue && !await _context.ShippingBatches.AnyAsync(x =>
                x.Id == request.ShippingBatchId, cancellationToken))
            return BadRequest("Shipping batch not found.");
        var movement = new ShipmentMovement
        {
            MovementCode = code,
            Mode = request.Mode,
            OperatorType = request.OperatorType,
            CarrierName = request.CarrierName.Trim(),
            CarrierCode = request.CarrierCode?.Trim().ToUpperInvariant(),
            FlightNumber = request.FlightNumber?.Trim().ToUpperInvariant(),
            VesselName = request.VesselName?.Trim(),
            VoyageNumber = request.VoyageNumber?.Trim().ToUpperInvariant(),
            MasterTransportReference = request.MasterTransportReference?.Trim().ToUpperInvariant(),
            OriginGateway = request.OriginGateway?.Trim(),
            DestinationGateway = request.DestinationGateway?.Trim(),
            OriginBranchCode = branches.Value.Origin,
            DestinationBranchCode = branches.Value.Destination,
            ScheduledDepartureAt = request.ScheduledDepartureAt?.ToUniversalTime(),
            EstimatedArrivalAt = request.EstimatedArrivalAt?.ToUniversalTime(),
            ShippingBatchId = request.ShippingBatchId,
            Notes = request.Notes?.Trim()
        };
        _context.ShipmentMovements.Add(movement);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { movementId = movement.Id }, movement);
    }

    [HttpPut("{movementId:guid}")]
    public async Task<ActionResult<ShipmentMovement>> Update(Guid movementId,
        UpdateShipmentMovementRequest request, CancellationToken cancellationToken)
    {
        var movement = await _context.ShipmentMovements.FirstOrDefaultAsync(x => x.Id == movementId,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment movement not found.");
        if (movement.Status is ShipmentMovementStatus.Departed or ShipmentMovementStatus.Arrived or
            ShipmentMovementStatus.Cancelled)
            return Conflict("A departed, arrived, or cancelled movement cannot be edited.");
        var error = Validate(movement.Mode, request.OperatorType, request.CarrierName, request.FlightNumber,
            request.ScheduledDepartureAt, request.EstimatedArrivalAt);
        if (error is not null) return BadRequest(error);
        var branches = await ResolveBranchesAsync(request.OriginBranchCode, request.DestinationBranchCode,
            cancellationToken);
        if (branches is null) return BadRequest("Active origin and destination branches are required.");
        if (request.ShippingBatchId.HasValue && !await _context.ShippingBatches.AnyAsync(x =>
                x.Id == request.ShippingBatchId, cancellationToken))
            return BadRequest("Shipping batch not found.");
        movement.OperatorType = request.OperatorType;
        movement.CarrierName = request.CarrierName.Trim();
        movement.CarrierCode = request.CarrierCode?.Trim().ToUpperInvariant();
        movement.FlightNumber = request.FlightNumber?.Trim().ToUpperInvariant();
        movement.VesselName = request.VesselName?.Trim();
        movement.VoyageNumber = request.VoyageNumber?.Trim().ToUpperInvariant();
        movement.MasterTransportReference = request.MasterTransportReference?.Trim().ToUpperInvariant();
        movement.OriginGateway = request.OriginGateway?.Trim();
        movement.DestinationGateway = request.DestinationGateway?.Trim();
        movement.OriginBranchCode = branches.Value.Origin;
        movement.DestinationBranchCode = branches.Value.Destination;
        movement.ScheduledDepartureAt = request.ScheduledDepartureAt?.ToUniversalTime();
        movement.EstimatedArrivalAt = request.EstimatedArrivalAt?.ToUniversalTime();
        movement.ShippingBatchId = request.ShippingBatchId;
        movement.Notes = request.Notes?.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(movement);
    }

    [HttpPatch("{movementId:guid}/status")]
    public async Task<ActionResult<ShipmentMovement>> ChangeStatus(Guid movementId,
        ChangeShipmentMovementStatusRequest request, CancellationToken cancellationToken)
    {
        var movement = await _context.ShipmentMovements.FirstOrDefaultAsync(x => x.Id == movementId,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment movement not found.");
        if (!CanTransition(movement.Status, request.Status))
            return Conflict($"Movement cannot change from {movement.Status} to {request.Status}.");
        movement.Status = request.Status;
        var eventAt = request.ActualEventAt?.ToUniversalTime() ?? DateTime.UtcNow;
        if (request.Status == ShipmentMovementStatus.Departed) movement.ActualDepartureAt = eventAt;
        if (request.Status == ShipmentMovementStatus.Arrived) movement.ActualArrivalAt = eventAt;
        if (!string.IsNullOrWhiteSpace(request.Note))
            movement.Notes = string.IsNullOrWhiteSpace(movement.Notes)
                ? request.Note.Trim() : $"{movement.Notes}\n{request.Note.Trim()}";
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(movement);
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] TransportMode? mode,
        [FromQuery] TransportOperatorType? operatorType,
        [FromQuery] ShipmentMovementStatus? status, [FromQuery] string? carrier,
        [FromQuery] string? branchCode, [FromQuery] string? search,
        [FromQuery] DateTime? departureFrom, [FromQuery] DateTime? departureTo,
        [FromQuery] bool? hasUnallocatedCapacity, [FromQuery] bool paged = true,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.ShipmentMovements.AsNoTracking().AsQueryable();
        if (mode.HasValue) query = query.Where(x => x.Mode == mode);
        if (operatorType.HasValue) query = query.Where(x => x.OperatorType == operatorType);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(carrier))
        {
            var value = carrier.Trim().ToLowerInvariant();
            query = query.Where(x => x.CarrierName.ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.OriginBranchCode == value || x.DestinationBranchCode == value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.MovementCode.ToLower().Contains(value) ||
                x.CarrierName.ToLower().Contains(value) ||
                (x.FlightNumber != null && x.FlightNumber.ToLower().Contains(value)) ||
                (x.VesselName != null && x.VesselName.ToLower().Contains(value)) ||
                (x.VoyageNumber != null && x.VoyageNumber.ToLower().Contains(value)) ||
                (x.MasterTransportReference != null && x.MasterTransportReference.ToLower().Contains(value)));
        }
        if (departureFrom.HasValue) query = query.Where(x => x.ScheduledDepartureAt >= departureFrom);
        if (departureTo.HasValue) query = query.Where(x => x.ScheduledDepartureAt < departureTo.Value.AddDays(1));
        if (hasUnallocatedCapacity == true)
            query = query.Where(x => x.Status == ShipmentMovementStatus.OpenForAllocation);
        var ordered = query.OrderByDescending(x => x.ScheduledDepartureAt ?? x.CreatedAt).ThenByDescending(x => x.Id);
        PagedResponse<ShipmentMovement>? pageResult = null;
        IReadOnlyList<ShipmentMovement> movements;
        if (paged)
        {
            pageResult = await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
            movements = pageResult.Items;
        }
        else movements = await ordered.Take(500).ToListAsync(cancellationToken);
        var ids = movements.Select(x => x.Id).ToList();
        var cargoStats = await _context.CargoItems.AsNoTracking().Where(x =>
                x.ShipmentMovementId.HasValue && ids.Contains(x.ShipmentMovementId.Value))
            .GroupBy(x => x.ShipmentMovementId!.Value).Select(x => new
            {
                MovementId = x.Key,
                CargoCount = x.Count(),
                ClientCount = x.Select(y => y.CustomerId).Distinct().Count(),
                TotalWeightKg = x.Sum(y => y.ActualWeightKg)
            }).ToDictionaryAsync(x => x.MovementId, cancellationToken);
        var containerStats = await _context.ShipmentContainers.AsNoTracking().Where(x =>
                x.ShipmentMovementId.HasValue && ids.Contains(x.ShipmentMovementId.Value))
            .GroupBy(x => x.ShipmentMovementId!.Value).Select(x => new { MovementId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.MovementId, x => x.Count, cancellationToken);
        var items = movements.Select(x => new
        {
            Movement = x,
            CargoCount = cargoStats.TryGetValue(x.Id, out var cargo) ? cargo.CargoCount : 0,
            ClientCount = cargoStats.TryGetValue(x.Id, out cargo) ? cargo.ClientCount : 0,
            TotalWeightKg = cargoStats.TryGetValue(x.Id, out cargo) ? cargo.TotalWeightKg : 0,
            ContainerCount = containerStats.GetValueOrDefault(x.Id)
        }).ToList();
        return pageResult is null ? Ok(items) : Ok(new { items, pageResult.Page, pageResult.PageSize,
            pageResult.HasPrevious, pageResult.HasNext, pageResult.Total, pageResult.TotalPages });
    }

    [HttpGet("{movementId:guid}")]
    public async Task<ActionResult> Get(Guid movementId, CancellationToken cancellationToken)
    {
        var movement = await _context.ShipmentMovements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == movementId,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment movement not found.");
        var containers = await _context.ShipmentContainers.AsNoTracking().Where(x =>
            x.ShipmentMovementId == movementId).OrderBy(x => x.ContainerNumber).ToListAsync(cancellationToken);
        var cargo = await (from item in _context.CargoItems.AsNoTracking()
            join customer in _context.Customers.AsNoTracking() on item.CustomerId equals customer.Id
            join invoice in _context.Invoices.AsNoTracking() on item.InvoiceId equals invoice.Id into invoiceGroup
            from invoice in invoiceGroup.DefaultIfEmpty()
            where item.ShipmentMovementId == movementId
            orderby customer.FullName, item.TrackingNumber
            select new { item.Id, item.TrackingNumber, item.MarkerCode, item.Description, item.Status,
                item.ActualWeightKg, item.InvoiceId, InvoiceNumber = invoice == null ? null : invoice.InvoiceNumber,
                item.ShipmentContainerId, item.AssignedTransportMode, CustomerId = customer.Id,
                customer.FullName, customer.Phone, customer.Email }).ToListAsync(cancellationToken);
        return Ok(new { movement, containers, cargo });
    }

    [HttpGet("{movementId:guid}/clients")]
    public async Task<ActionResult> Clients(Guid movementId, [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (!await _context.ShipmentMovements.AsNoTracking().AnyAsync(x => x.Id == movementId, cancellationToken))
            return NotFound();
        var query = from item in _context.CargoItems.AsNoTracking()
            join customer in _context.Customers.AsNoTracking() on item.CustomerId equals customer.Id
            where item.ShipmentMovementId == movementId
            select new { item, customer };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.customer.FullName.ToLower().Contains(value) ||
                (x.customer.Email != null && x.customer.Email.ToLower().Contains(value)) ||
                x.customer.Phone.Contains(value));
        }
        var clients = await query.GroupBy(x => new { x.customer.Id, x.customer.FullName,
                x.customer.Phone, x.customer.Email })
            .Select(x => new { CustomerId = x.Key.Id, x.Key.FullName, x.Key.Phone, x.Key.Email,
                CargoCount = x.Count(), TotalWeightKg = x.Sum(y => y.item.ActualWeightKg) })
            .OrderBy(x => x.FullName).Take(500).ToListAsync(cancellationToken);
        return Ok(clients);
    }

    private async Task<(string Origin, string Destination)?> ResolveBranchesAsync(string origin,
        string destination, CancellationToken cancellationToken)
    {
        var originCode = origin.Trim().ToUpperInvariant();
        var destinationCode = destination.Trim().ToUpperInvariant();
        if (originCode == destinationCode) return null;
        var count = await _context.Branches.AsNoTracking().CountAsync(x => x.IsActive &&
            (x.Code == originCode || x.Code == destinationCode), cancellationToken);
        return count == 2 ? (originCode, destinationCode) : null;
    }

    private static string? Validate(TransportMode mode, TransportOperatorType operatorType,
        string carrierName, string? flightNumber,
        DateTime? departure, DateTime? arrival)
    {
        if (mode is not (TransportMode.Air or TransportMode.Sea)) return "Movement mode must be Air or Sea.";
        if (operatorType == TransportOperatorType.BigsamLocalDelivery)
            return "Air and sea movements must identify a third-party carrier or partner forwarder. Bigsam local delivery belongs to the pickup/delivery workflow.";
        if (string.IsNullOrWhiteSpace(carrierName)) return "External carrier, airline, or partner forwarder name is required.";
        if (mode == TransportMode.Air && string.IsNullOrWhiteSpace(flightNumber))
            return "An air movement requires a flight number.";
        return departure.HasValue && arrival.HasValue && arrival <= departure
            ? "Estimated arrival must be after scheduled departure." : null;
    }

    private static bool CanTransition(ShipmentMovementStatus current, ShipmentMovementStatus next) =>
        current == next || current switch
        {
            ShipmentMovementStatus.Planning => next is ShipmentMovementStatus.OpenForAllocation or ShipmentMovementStatus.Cancelled,
            ShipmentMovementStatus.OpenForAllocation => next is ShipmentMovementStatus.Planning or ShipmentMovementStatus.Closed or
                ShipmentMovementStatus.Departed or ShipmentMovementStatus.Cancelled,
            ShipmentMovementStatus.Closed => next is ShipmentMovementStatus.OpenForAllocation or
                ShipmentMovementStatus.Departed or ShipmentMovementStatus.Cancelled,
            ShipmentMovementStatus.Departed => next == ShipmentMovementStatus.Arrived,
            _ => false
        };
}

[ApiController]
[Authorize(Policy = "OperationsTeam")]
[Route("api/operations/shipment-allocations")]
public sealed class ShipmentAllocationsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICargoAllocationService _allocations;
    public ShipmentAllocationsController(IApplicationDbContext context, ICargoAllocationService allocations)
    { _context = context; _allocations = allocations; }

    private Guid CurrentUserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id)
        ? id : throw new UnauthorizedAccessException("User identity is missing.");

    [HttpPost]
    public async Task<ActionResult<CargoAllocationResult>> Allocate(AllocateCargoRequest request,
        CancellationToken cancellationToken) => Ok(await _allocations.AllocateAsync(new AllocateCargoCommand(
            request.InvoiceId, request.CargoIds, request.ShipmentMovementId, request.ShipmentContainerId,
            request.Reason), CurrentUserId, cancellationToken));

    [HttpDelete("cargo/{cargoId:guid}")]
    public async Task<IActionResult> Unassign(Guid cargoId, UnassignCargoRequest request,
        CancellationToken cancellationToken)
    {
        await _allocations.UnassignAsync(cargoId, request.Reason, CurrentUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult> History([FromQuery] Guid? cargoId, [FromQuery] Guid? invoiceId,
        [FromQuery] Guid? movementId, [FromQuery] bool currentOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.CargoAllocations.AsNoTracking().AsQueryable();
        if (cargoId.HasValue) query = query.Where(x => x.CargoItemId == cargoId);
        if (invoiceId.HasValue) query = query.Where(x => x.InvoiceId == invoiceId);
        if (movementId.HasValue) query = query.Where(x => x.ShipmentMovementId == movementId);
        if (currentOnly) query = query.Where(x => x.UnassignedAt == null);
        return Ok(await query.OrderByDescending(x => x.AssignedAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }
}
