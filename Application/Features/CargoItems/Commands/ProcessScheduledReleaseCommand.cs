using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CargoItems.Commands;

public record ProcessScheduledReleaseCommand(Guid BatchId) : IRequest<ShippingBatchDto>;

public class ProcessScheduledReleaseCommandHandler : IRequestHandler<ProcessScheduledReleaseCommand, ShippingBatchDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IOmniChannelNotificationService _notificationService;

    public ProcessScheduledReleaseCommandHandler(IApplicationDbContext context, IOmniChannelNotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<ShippingBatchDto> Handle(ProcessScheduledReleaseCommand request, CancellationToken cancellationToken)
    {
        var batch = await _context.ShippingBatches
            .FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken)
            ?? throw new ArgumentException($"Shipping batch with ID {request.BatchId} not found.");

        if (batch.Status == BatchStatus.Released || batch.Status == BatchStatus.Delivered)
            throw new InvalidOperationException($"Batch is already completed with status: {batch.Status}.");

        var cargoItems = await _context.CargoItems
            .Where(c => c.BatchId == request.BatchId)
            .ToListAsync(cancellationToken);

        var externalMovement = await _context.ShipmentMovements.AsNoTracking()
            .Where(x => x.ShippingBatchId == request.BatchId)
            .OrderByDescending(x => x.ScheduledDepartureAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Drive batch state transitions
        switch (batch.Status)
        {
            case BatchStatus.Draft:
            case BatchStatus.Locked:
                if (externalMovement is null)
                    throw new InvalidOperationException("Link this internal consolidation batch to the confirmed third-party carrier movement before dispatch.");
                if (string.IsNullOrWhiteSpace(externalMovement.MasterTransportReference))
                    throw new InvalidOperationException("Record the carrier-issued master AWB, bill of lading, or booking reference before dispatch.");
                batch.DispatchBatch(externalMovement.MasterTransportReference);
                foreach (var item in cargoItems)
                    item.UpdateStatus(CargoStatus.InTransit);
                break;

            case BatchStatus.Dispatched:
                batch.ArriveAtCustoms();
                foreach (var item in cargoItems)
                    item.UpdateStatus(CargoStatus.ArrivedAtDestinationHub);
                break;

            case BatchStatus.ArrivedAtCustoms:
                batch.ReleaseFromCustoms();
                foreach (var item in cargoItems)
                    item.UpdateStatus(CargoStatus.OutForDelivery);
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Alert customers
        try
        {
            var customerIds = cargoItems.Select(c => c.CustomerId).Distinct().ToList();
            var customers = await _context.Customers
                .Where(c => customerIds.Contains(c.Id))
                .ToListAsync(cancellationToken);

            await _notificationService.SendBatchDispatchAlertAsync(customers, batch, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Batch alert dispatch failed silently: {ex.Message}");
        }

        return new ShippingBatchDto(
            batch.Id, batch.BatchCode, batch.AirwayBillNumber, batch.MaxWeightCapacityKg,
            batch.ScheduledReleaseDate, batch.Status, batch.OriginHub, batch.DestinationHub,
            batch.ConsolidationType,
            batch.VesselName, batch.VoyageNumber, batch.PortOfLoading, batch.PortOfDischarge,
            batch.FreezeTracking, batch.CreatedAt
        );
    }
}
