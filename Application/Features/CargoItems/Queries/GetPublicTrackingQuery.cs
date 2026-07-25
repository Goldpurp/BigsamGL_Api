using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CargoItems.Queries;

public record GetPublicTrackingQuery(string TrackingNumber) : IRequest<PublicTrackingResponse>;

public class GetPublicTrackingQueryHandler : IRequestHandler<GetPublicTrackingQuery, PublicTrackingResponse>
{
    private readonly IApplicationDbContext _context;

    public GetPublicTrackingQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PublicTrackingResponse> Handle(GetPublicTrackingQuery request, CancellationToken cancellationToken)
    {
        var trackingNumber = request.TrackingNumber.Trim().ToUpperInvariant();
        var cargoItem = await _context.CargoItems.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TrackingNumber == trackingNumber ||
                c.MarkerCode == trackingNumber, cancellationToken)
            ?? throw new KeyNotFoundException("Shipment was not found.");

        var milestones = await _context.ShipmentMilestones.AsNoTracking()
            .Where(x => x.CargoItemId == cargoItem.Id && x.Visibility == MilestoneVisibility.CustomerVisible)
            .OrderBy(x => x.OccurredAt)
            .Select(x => new PublicTrackingMilestoneDto(x.Id, x.OccurredAt, x.Status.ToString(), x.EventType,
                x.Title, x.Description, x.Location, x.Source, x.EstimatedDeliveryAt,
                x.IsException, x.IssueCode, x.IsCorrected))
            .ToListAsync(cancellationToken);

        var canonicalShipment = cargoItem.InvoiceId.HasValue
            ? await _context.CustomerShipments.AsNoTracking()
                .Where(x => x.InvoiceId == cargoItem.InvoiceId.Value)
                .Select(x => new
                {
                    x.OriginCity,
                    x.DestinationCityOrState,
                    x.Method,
                    x.DeliveryAddress
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var movement = cargoItem.ShipmentMovementId.HasValue
            ? await _context.ShipmentMovements.AsNoTracking()
                .Where(x => x.Id == cargoItem.ShipmentMovementId.Value)
                .Select(x => new
                {
                    x.OriginGateway,
                    x.DestinationGateway,
                    x.OriginBranchCode,
                    x.DestinationBranchCode,
                    x.CarrierName,
                    x.FlightNumber,
                    x.VesselName,
                    x.VoyageNumber,
                    x.MasterTransportReference,
                    x.EstimatedArrivalAt,
                    x.Mode
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var batch = cargoItem.BatchId.HasValue
            ? await _context.ShippingBatches.AsNoTracking()
                .Where(x => x.Id == cargoItem.BatchId.Value)
                .Select(x => new { x.OriginHub, x.DestinationHub })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var latest = milestones.LastOrDefault();
        var effectiveStatus = latest is not null &&
            Enum.TryParse<CargoStatus>(latest.Status, true, out var milestoneStatus)
                ? milestoneStatus
                : cargoItem.Status;
        var customerFacingStatus = latest?.Status ?? cargoItem.Status.ToString();
        var milestoneDetails = latest?.Description ?? "Your cargo has been logged and is being prepared for its next movement.";
        var lastUpdated = milestones.Count > 0 ? milestones.Max(x => x.OccurredAt) : cargoItem.LastModifiedAt ?? cargoItem.CreatedAt;
        var origin = FirstValue(canonicalShipment?.OriginCity, movement?.OriginGateway,
            movement?.OriginBranchCode, batch?.OriginHub, milestones.FirstOrDefault()?.Location,
            "Origin hub");
        var destination = FirstValue(canonicalShipment?.DestinationCityOrState,
            movement?.DestinationGateway, movement?.DestinationBranchCode,
            batch?.DestinationHub, "Destination hub");
        var mode = cargoItem.AssignedTransportMode ?? movement?.Mode ??
            canonicalShipment?.Method switch
            {
                CustomerShipmentMethod.Air => TransportMode.Air,
                CustomerShipmentMethod.Sea => TransportMode.Sea,
                _ => null
            };
        var estimatedDelivery = milestones.LastOrDefault(x =>
            x.EstimatedDeliveryAt.HasValue)?.EstimatedDeliveryAt ??
            movement?.EstimatedArrivalAt;
        var currentLocation = milestones.LastOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.Location))?.Location ?? origin;
        var hasException = milestones.Any(x => x.IsException && !x.IsCorrected) ||
            effectiveStatus is CargoStatus.Delayed or CargoStatus.OnHold;
        var issueCode = milestones.LastOrDefault(x =>
            x.IsException && !x.IsCorrected)?.IssueCode;
        var carrierName = FirstValue(movement?.CarrierName, cargoItem.CarrierProvider,
            "BiGSAM Global Logistics");
        var transportReference = FirstValueOrNull(movement?.FlightNumber,
            movement?.VesselName, movement?.VoyageNumber, movement?.MasterTransportReference,
            cargoItem.ExternalTrackingNumber);

        return new PublicTrackingResponse(
            cargoItem.TrackingNumber,
            cargoItem.MarkerCode,
            cargoItem.Description,
            origin,
            destination,
            customerFacingStatus,
            milestoneDetails,
            lastUpdated,
            currentLocation,
            estimatedDelivery,
            Progress(effectiveStatus),
            hasException,
            issueCode,
            string.IsNullOrWhiteSpace(canonicalShipment?.DeliveryAddress)
                ? DeliveryMethod.HubPickup
                : DeliveryMethod.AddressDelivery,
            mode,
            carrierName,
            transportReference,
            milestones
        );
    }

    private static int Progress(CargoStatus status) => status switch
    {
        CargoStatus.Logged => 8,
        CargoStatus.PickupScheduled => 12,
        CargoStatus.PickedUp or CargoStatus.IncomingToWarehouse => 20,
        CargoStatus.ReceivedAtWarehouse => 28,
        CargoStatus.AssignedToBatch => 38,
        CargoStatus.InTransit => 60,
        CargoStatus.ArrivedAtDestinationHub => 78,
        CargoStatus.OutForDelivery => 90,
        CargoStatus.Delivered => 100,
        CargoStatus.Delayed or CargoStatus.OnHold => 55,
        CargoStatus.Cancelled => 0,
        _ => 10
    };

    private static string FirstValue(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

    private static string? FirstValueOrNull(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
}
