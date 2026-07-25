using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class ShipmentContainer : BaseEntity
{
    public Guid? ShippingBatchId { get; set; }
    public Guid? ShipmentMovementId { get; set; }
    public string Provider { get; set; } = "MSC";
    public string ContainerNumber { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public ContainerType Type { get; set; } = ContainerType.NotSpecified;
    public string? CarrierBookingReference { get; set; }
    public string? BillOfLadingNumber { get; set; }
    public string Status { get; set; } = "Registered";
    public string CurrentLocation { get; set; } = string.Empty;
    public DateTime? EstimatedArrivalAt { get; set; }
    public DateTime? LastEventAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
