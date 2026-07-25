using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class ShipmentMovement : BaseEntity
{
    public string MovementCode { get; set; } = string.Empty;
    public TransportMode Mode { get; set; }
    public ShipmentMovementStatus Status { get; set; } = ShipmentMovementStatus.Planning;
    public TransportOperatorType OperatorType { get; set; } = TransportOperatorType.ThirdPartyCarrier;
    public string CarrierName { get; set; } = string.Empty;
    public string? CarrierCode { get; set; }
    public string? FlightNumber { get; set; }
    public string? VesselName { get; set; }
    public string? VoyageNumber { get; set; }
    public string? MasterTransportReference { get; set; }
    public string? OriginGateway { get; set; }
    public string? DestinationGateway { get; set; }
    public string OriginBranchCode { get; set; } = string.Empty;
    public string DestinationBranchCode { get; set; } = string.Empty;
    public DateTime? ScheduledDepartureAt { get; set; }
    public DateTime? EstimatedArrivalAt { get; set; }
    public DateTime? ActualDepartureAt { get; set; }
    public DateTime? ActualArrivalAt { get; set; }
    public Guid? ShippingBatchId { get; set; }
    public string? Notes { get; set; }
}
