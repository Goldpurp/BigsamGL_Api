using Domain.Enums;

namespace Api.Controllers;

public sealed record CreateShipmentMovementRequest(string MovementCode, TransportMode Mode,
    string CarrierName, string? CarrierCode, string? FlightNumber, string? VesselName,
    string? VoyageNumber, string OriginBranchCode, string DestinationBranchCode,
    DateTime? ScheduledDepartureAt, DateTime? EstimatedArrivalAt, Guid? ShippingBatchId,
    string? Notes, TransportOperatorType OperatorType = TransportOperatorType.ThirdPartyCarrier,
    string? MasterTransportReference = null, string? OriginGateway = null,
    string? DestinationGateway = null);
public sealed record UpdateShipmentMovementRequest(string CarrierName, string? CarrierCode,
    string? FlightNumber, string? VesselName, string? VoyageNumber, string OriginBranchCode,
    string DestinationBranchCode, DateTime? ScheduledDepartureAt, DateTime? EstimatedArrivalAt,
    Guid? ShippingBatchId, string? Notes,
    TransportOperatorType OperatorType = TransportOperatorType.ThirdPartyCarrier,
    string? MasterTransportReference = null, string? OriginGateway = null,
    string? DestinationGateway = null);
public sealed record ChangeShipmentMovementStatusRequest(ShipmentMovementStatus Status,
    DateTime? ActualEventAt, string? Note);
public sealed record AllocateCargoRequest(Guid InvoiceId, IReadOnlyList<Guid> CargoIds,
    Guid ShipmentMovementId, Guid? ShipmentContainerId, string? Reason);
public sealed record UnassignCargoRequest(string Reason);
