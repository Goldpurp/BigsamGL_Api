using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class ShipmentMilestone : BaseEntity
{
    public Guid CargoItemId { get; set; }
    public CargoStatus Status { get; set; }
    public ShipmentEventType EventType { get; set; } = ShipmentEventType.General;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public MilestoneVisibility Visibility { get; set; } = MilestoneVisibility.CustomerVisible;
    public string Source { get; set; } = "BiGSAM";
    public string? ExternalEventId { get; set; }
    public string? CarrierProvider { get; set; }
    public bool IsException { get; set; }
    public string? IssueCode { get; set; }
    public DateTime? EstimatedDeliveryAt { get; set; }
    public DateTime? CustomerNotifiedAt { get; set; }
    public bool IsCorrected { get; set; }
    public string? RevisionReason { get; set; }
}
