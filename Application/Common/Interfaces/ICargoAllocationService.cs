using Domain.Entities;

namespace Application.Common.Interfaces;

public sealed record AllocateCargoCommand(Guid InvoiceId, IReadOnlyList<Guid> CargoIds,
    Guid ShipmentMovementId, Guid? ShipmentContainerId, string? Reason);

public sealed record CargoAllocationResult(Guid ShipmentMovementId, int AssignedCargoCount,
    IReadOnlyList<CargoAllocation> Allocations);

public interface ICargoAllocationService
{
    Task<CargoAllocationResult> AllocateAsync(AllocateCargoCommand command, Guid actorUserId,
        CancellationToken cancellationToken);
    Task UnassignAsync(Guid cargoId, string reason, Guid actorUserId, CancellationToken cancellationToken);
}
