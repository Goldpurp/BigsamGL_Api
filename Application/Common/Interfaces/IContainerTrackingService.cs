using Domain.Entities;

namespace Application.Common.Interfaces;

public record ContainerSyncResult(Guid ContainerId, string ContainerNumber, int EventsReceived,
    int NewEventsStored, int CargoItemsUpdated, int ClientsNotified, bool IsMock, DateTime SyncedAt);

public interface IContainerTrackingService
{
    Task<ContainerSyncResult> SyncAsync(Guid containerId, bool notifyClients, CancellationToken cancellationToken);
    Task<ContainerSyncResult> RecordManualEventAsync(Guid containerId, MscTrackingEvent trackingEvent,
        bool notifyClients, CancellationToken cancellationToken);
}
