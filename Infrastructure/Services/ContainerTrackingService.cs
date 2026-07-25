using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ContainerTrackingService : IContainerTrackingService
{
    private readonly IApplicationDbContext _context;
    private readonly IMscTrackingGateway _msc;
    private readonly IShipmentTrackingService _tracking;

    public ContainerTrackingService(IApplicationDbContext context, IMscTrackingGateway msc,
        IShipmentTrackingService tracking)
    {
        _context = context; _msc = msc; _tracking = tracking;
    }

    public async Task<ContainerSyncResult> SyncAsync(Guid containerId, bool notifyClients,
        CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(containerId, cancellationToken);
        var snapshot = await _msc.FetchContainerAsync(container.ContainerNumber,
            container.CarrierBookingReference, container.BillOfLadingNumber, cancellationToken);
        container.Status = snapshot.Status; container.CurrentLocation = snapshot.CurrentLocation;
        container.EstimatedArrivalAt = snapshot.EstimatedArrivalAt; container.LastSyncedAt = DateTime.UtcNow;
        return await ProcessAsync(container, snapshot.Events, notifyClients, snapshot.IsMock, cancellationToken);
    }

    public async Task<ContainerSyncResult> RecordManualEventAsync(Guid containerId,
        MscTrackingEvent trackingEvent, bool notifyClients, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(containerId, cancellationToken);
        container.Status = trackingEvent.EventTypeCode; container.CurrentLocation = trackingEvent.Location;
        container.EstimatedArrivalAt = trackingEvent.EstimatedArrivalAt; container.LastSyncedAt = DateTime.UtcNow;
        return await ProcessAsync(container, new[] { trackingEvent }, notifyClients, false, cancellationToken);
    }

    private async Task<ContainerSyncResult> ProcessAsync(ShipmentContainer container,
        IReadOnlyList<MscTrackingEvent> received, bool notifyClients, bool isMock, CancellationToken cancellationToken)
    {
        var receivedIds = received.Select(x => x.ExternalEventId).Distinct().ToList();
        var existingIds = await _context.CarrierTrackingEvents.AsNoTracking().Where(x =>
                x.Provider == container.Provider && receivedIds.Contains(x.ExternalEventId))
            .Select(x => x.ExternalEventId).ToListAsync(cancellationToken);
        var newEvents = received.Where(x => !existingIds.Contains(x.ExternalEventId)).OrderBy(x => x.OccurredAt).ToList();
        var cargo = await _context.CargoItems.Where(x => x.ShipmentContainerId == container.Id).ToListAsync(cancellationToken);
        var updated = 0; var notifiedCustomers = new HashSet<Guid>();

        foreach (var item in newEvents)
        {
            var stored = new CarrierTrackingEvent
            {
                ShipmentContainerId = container.Id, Provider = container.Provider,
                ExternalEventId = item.ExternalEventId, EventClassifier = item.EventClassifier,
                EventTypeCode = item.EventTypeCode, EventStatus = item.EventStatus,
                Description = item.Description, Location = item.Location, OccurredAt = item.OccurredAt,
                EstimatedArrivalAt = item.EstimatedArrivalAt, RawPayloadJson = item.RawPayloadJson,
                ProcessedAt = DateTime.UtcNow, PropagatedCargoCount = cargo.Count
            };
            _context.CarrierTrackingEvents.Add(stored);
            var (status, eventType) = Map(item.EventTypeCode);
            foreach (var cargoItem in cargo)
            {
                var shouldNotify = notifyClients && notifiedCustomers.Add(cargoItem.CustomerId);
                await _tracking.RecordAsync(cargoItem.Id, new ShipmentTrackingInput(status, eventType,
                    $"MSC container update: {item.EventTypeCode}", item.Description, item.Location,
                    item.OccurredAt, MilestoneVisibility.CustomerVisible, item.EstimatedArrivalAt,
                    shouldNotify, false, null, isMock ? "MSC Local Tracking" : "MSC Track & Trace",
                    container.Provider, item.ExternalEventId), cancellationToken);
                updated++;
            }
            container.LastEventAt = item.OccurredAt;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return new ContainerSyncResult(container.Id, container.ContainerNumber, received.Count,
            newEvents.Count, updated, notifiedCustomers.Count, isMock, container.LastSyncedAt ?? DateTime.UtcNow);
    }

    private async Task<ShipmentContainer> GetContainerAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.ShipmentContainers.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken)
        ?? throw new KeyNotFoundException("Shipment container not found.");

    private static (CargoStatus, ShipmentEventType) Map(string code) => code.Trim().ToUpperInvariant() switch
    {
        "GTIN" or "GATE_IN" => (CargoStatus.ReceivedAtWarehouse, ShipmentEventType.WarehouseReceived),
        "LOAD" or "DEPA" or "DEPARTURE" => (CargoStatus.InTransit, ShipmentEventType.DepartedOrigin),
        "ARRI" or "ARRIVAL" => (CargoStatus.ArrivedAtDestinationHub, ShipmentEventType.ArrivedAtHub),
        "DISC" or "DISCHARGE" => (CargoStatus.ArrivedAtDestinationHub, ShipmentEventType.ArrivedAtHub),
        "GTOT" or "GATE_OUT" => (CargoStatus.OutForDelivery, ShipmentEventType.OutForDelivery),
        "DELY" or "DELIVERED" => (CargoStatus.Delivered, ShipmentEventType.Delivered),
        "DELAY" => (CargoStatus.Delayed, ShipmentEventType.Delayed),
        _ => (CargoStatus.InTransit, ShipmentEventType.CarrierUpdate)
    };
}
