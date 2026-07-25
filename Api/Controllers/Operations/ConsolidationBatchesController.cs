using Application.Common.Interfaces;
using Application.DTOs;
using Application.Features.CargoItems.Commands;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Authorize(Policy = "OperationsTeam")]
[Route("api/operations/consolidation-batches")]
public sealed class ConsolidationBatchesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public ConsolidationBatchesController(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    [HttpPost]
    [HttpPost("/api/cargo/batches")]
    public async Task<ActionResult<ShippingBatchDto>> Create(CreateBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MaxWeightCapacityKg <= 0) return BadRequest("Maximum batch weight must be positive.");
        var code = request.BatchCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return BadRequest("Internal consolidation batch code is required.");
        if (await _context.ShippingBatches.AnyAsync(x => x.BatchCode == code, cancellationToken))
            return Conflict("Consolidation batch code is already in use.");

        var batch = new ShippingBatch(Guid.NewGuid(), code, request.AirwayBillNumber?.Trim(),
            request.MaxWeightCapacityKg, request.ScheduledReleaseDate.ToUniversalTime(),
            request.OriginHub.Trim(), request.DestinationHub.Trim())
        {
            ConsolidationType = request.ConsolidationType,
            // Retained only for clients of the legacy route. Carrier data for new work is
            // maintained on the linked ShipmentMovement.
            VesselName = request.VesselName?.Trim() ?? string.Empty,
            VoyageNumber = request.VoyageNumber?.Trim() ?? string.Empty,
            PortOfLoading = request.PortOfLoading?.Trim() ?? string.Empty,
            PortOfDischarge = request.PortOfDischarge?.Trim() ?? string.Empty
        };
        _context.ShippingBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/consolidation-batches/{batch.Id}", batch.ToDto());
    }

    [HttpGet]
    [HttpGet("/api/cargo/batches")]
    public async Task<ActionResult> List([FromQuery] BatchStatus? status,
        [FromQuery] ConsolidationType? consolidationType, [FromQuery] string? search,
        [FromQuery] DateTime? releaseFrom, [FromQuery] DateTime? releaseTo,
        [FromQuery] bool paged = true, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ShippingBatches.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (consolidationType.HasValue) query = query.Where(x => x.ConsolidationType == consolidationType);
        if (releaseFrom.HasValue) query = query.Where(x => x.ScheduledReleaseDate >= releaseFrom.Value.ToUniversalTime());
        if (releaseTo.HasValue) query = query.Where(x => x.ScheduledReleaseDate < releaseTo.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.BatchCode.ToLower().Contains(term) ||
                x.OriginHub.ToLower().Contains(term) || x.DestinationHub.ToLower().Contains(term) ||
                (x.AirwayBillNumber != null && x.AirwayBillNumber.ToLower().Contains(term)));
        }
        var ordered = query.OrderByDescending(x => x.ScheduledReleaseDate).ThenByDescending(x => x.Id);
        if (paged)
        {
            var result = await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
            return Ok(new PagedResponse<ShippingBatchDto>(result.Items.Select(x => x.ToDto()).ToList(),
                result.Page, result.PageSize, result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
        }
        return Ok((await ordered.Take(500).ToListAsync(cancellationToken)).Select(x => x.ToDto()).ToList());
    }

    [HttpGet("{batchId:guid}")]
    public async Task<ActionResult> Get(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _context.ShippingBatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == batchId,
            cancellationToken) ?? throw new KeyNotFoundException("Consolidation batch not found.");
        var movement = await _context.ShipmentMovements.AsNoTracking().Where(x => x.ShippingBatchId == batchId)
            .OrderByDescending(x => x.ScheduledDepartureAt ?? x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var cargo = await _context.CargoItems.AsNoTracking().Where(x => x.BatchId == batchId)
            .Select(x => new { x.Id, x.TrackingNumber, x.MarkerCode, x.CustomerId, x.Description,
                x.ActualWeightKg, x.Status, x.ShipmentMovementId, x.ShipmentContainerId })
            .OrderBy(x => x.TrackingNumber).Take(1000).ToListAsync(cancellationToken);
        return Ok(new
        {
            Batch = batch.ToDto(),
            ExternalTransport = movement,
            BigsamRole = "FreightForwarderAndConsolidator",
            Cargo = cargo,
            CargoCount = cargo.Count,
            TotalWeightKg = cargo.Sum(x => x.ActualWeightKg)
        });
    }

    [HttpPost("{batchId:guid}/cargo/{itemId:guid}")]
    [HttpPost("/api/cargo/batches/{batchId:guid}/add/{itemId:guid}")]
    public async Task<ActionResult<CargoItemDto>> AddCargo(Guid batchId, Guid itemId,
        CancellationToken cancellationToken)
    {
        var batch = await _context.ShippingBatches.FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Consolidation batch not found.");
        var item = await _context.CargoItems.FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Cargo item not found.");
        var currentWeight = await _context.CargoItems.Where(x => x.BatchId == batchId)
            .SumAsync(x => (decimal?)x.ActualWeightKg, cancellationToken) ?? 0;
        batch.AddCargoItem(item, currentWeight);
        item.AssignToBatch(batch.Id);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(item.ToDto());
    }

    [HttpDelete("{batchId:guid}/cargo/{itemId:guid}")]
    [HttpPost("/api/cargo/batches/{batchId:guid}/remove/{itemId:guid}")]
    public async Task<ActionResult<CargoItemDto>> RemoveCargo(Guid batchId, Guid itemId,
        CancellationToken cancellationToken)
    {
        var item = await _context.CargoItems.FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Cargo item not found.");
        if (item.BatchId != batchId) return BadRequest("Cargo item is not assigned to this consolidation batch.");
        item.RemoveFromBatch();
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(item.ToDto());
    }

    [HttpPost("{batchId:guid}/release")]
    [HttpPost("/api/cargo/batches/{batchId:guid}/release")]
    public async Task<ActionResult<ShippingBatchDto>> Release(Guid batchId,
        CancellationToken cancellationToken) => Ok(await _mediator.Send(
            new ProcessScheduledReleaseCommand(batchId), cancellationToken));

    [HttpPost("{batchId:guid}/freeze")]
    [HttpPost("/api/cargo/batches/{batchId:guid}/freeze")]
    public async Task<ActionResult<ShippingBatchDto>> Freeze(Guid batchId, [FromQuery] bool freeze,
        CancellationToken cancellationToken)
    {
        var batch = await _context.ShippingBatches.FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Consolidation batch not found.");
        batch.FreezeTracking = freeze;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(batch.ToDto());
    }
}
