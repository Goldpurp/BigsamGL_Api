using Domain.Common;

namespace Domain.Entities;

public sealed class CarrierTrackingEvent : BaseEntity
{
    public Guid ShipmentContainerId { get; set; }
    public string Provider { get; set; } = "MSC";
    public string ExternalEventId { get; set; } = string.Empty;
    public string EventClassifier { get; set; } = string.Empty;
    public string EventTypeCode { get; set; } = string.Empty;
    public string EventStatus { get; set; } = "NEW";
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? EstimatedArrivalAt { get; set; }
    public string RawPayloadJson { get; set; } = "{}";
    public DateTime ProcessedAt { get; set; }
    public int PropagatedCargoCount { get; set; }
}
