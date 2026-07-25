using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class CargoAllocation : BaseEntity
{
    public Guid CargoItemId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ShipmentMovementId { get; set; }
    public Guid? ShipmentContainerId { get; set; }
    public TransportMode Mode { get; set; }
    public ContainerType? ContainerType { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string AssignedByUserId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }
    public string? UnassignedByUserId { get; set; }
    public string? Reason { get; set; }
}
