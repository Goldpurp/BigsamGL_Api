using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces;

public record ShipmentTrackingInput(CargoStatus Status, ShipmentEventType EventType, string Title,
    string Description, string Location, DateTime OccurredAt, MilestoneVisibility Visibility,
    DateTime? EstimatedDeliveryAt, bool NotifyCustomer, bool IsException, string? IssueCode,
    string Source, string? CarrierProvider = null, string? ExternalEventId = null);

public record BatchTrackingResult(Guid BatchId, int CargoItemsUpdated, int ClientsNotified,
    IReadOnlyList<Guid> MilestoneIds);

public interface IShipmentTrackingService
{
    Task<ShipmentMilestone> RecordAsync(Guid cargoId, ShipmentTrackingInput input,
        CancellationToken cancellationToken);
    Task<ShipmentMilestone> CorrectAsync(Guid cargoId, Guid milestoneId, ShipmentTrackingInput input,
        string revisionReason, CancellationToken cancellationToken);
    Task<BatchTrackingResult> RecordBatchAsync(Guid batchId, ShipmentTrackingInput input,
        CancellationToken cancellationToken);
}
