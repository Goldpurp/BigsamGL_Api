using System.Data;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class CargoAllocationService : ICargoAllocationService
{
    private readonly ApplicationDbContext _context;
    public CargoAllocationService(ApplicationDbContext context) => _context = context;

    public async Task<CargoAllocationResult> AllocateAsync(AllocateCargoCommand command, Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var cargoIds = command.CargoIds.Distinct().ToList();
        if (cargoIds.Count == 0 || cargoIds.Count > 500)
            throw new ArgumentException("Select between 1 and 500 cargo items per assignment.");
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable,
                cancellationToken);
            var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == command.InvoiceId,
                cancellationToken) ?? throw new KeyNotFoundException("Invoice not found.");
            if (invoice.Status == InvoiceStatus.Void)
                throw new InvalidOperationException("Cargo cannot be assigned from a void invoice.");
            var movement = await _context.ShipmentMovements.FirstOrDefaultAsync(x =>
                x.Id == command.ShipmentMovementId, cancellationToken)
                ?? throw new KeyNotFoundException("Shipment movement not found.");
            if (movement.Status is not (ShipmentMovementStatus.Planning or ShipmentMovementStatus.OpenForAllocation))
                throw new InvalidOperationException("This shipment movement is not open for cargo allocation.");
            if (movement.Mode is not (TransportMode.Air or TransportMode.Sea))
                throw new InvalidOperationException("Cargo allocation currently supports air and sea movements.");

            ShipmentContainer? container = null;
            if (movement.Mode == TransportMode.Sea)
            {
                if (!command.ShipmentContainerId.HasValue)
                    throw new ArgumentException("A sea shipment allocation must select a container.");
                container = await _context.ShipmentContainers.FirstOrDefaultAsync(x =>
                    x.Id == command.ShipmentContainerId && x.IsActive, cancellationToken)
                    ?? throw new KeyNotFoundException("Active shipment container not found.");
                if (container.ShipmentMovementId != movement.Id)
                    throw new InvalidOperationException("The selected container does not belong to this sea movement.");
            }
            else if (command.ShipmentContainerId.HasValue)
                throw new ArgumentException("Air cargo is assigned to an airline movement, not a sea container.");

            var cargo = await _context.CargoItems.Where(x => cargoIds.Contains(x.Id)).ToListAsync(cancellationToken);
            if (cargo.Count != cargoIds.Count) throw new KeyNotFoundException("One or more cargo items were not found.");
            if (cargo.Any(x => x.CustomerId != invoice.CustomerId))
                throw new InvalidOperationException("Every selected cargo item must belong to the invoice client.");
            if (cargo.Any(x => x.InvoiceId.HasValue && x.InvoiceId != invoice.Id))
                throw new InvalidOperationException("One or more cargo items are already linked to a different invoice.");

            var existing = await _context.CargoAllocations.Where(x => cargoIds.Contains(x.CargoItemId) &&
                x.UnassignedAt == null).ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var old in existing)
            {
                old.UnassignedAt = now;
                old.UnassignedByUserId = actorUserId.ToString();
                old.Reason = string.IsNullOrWhiteSpace(command.Reason)
                    ? "Reassigned to another shipment movement." : command.Reason.Trim();
            }
            if (existing.Count > 0) await _context.SaveChangesAsync(cancellationToken);

            var allocations = new List<CargoAllocation>(cargo.Count);
            foreach (var item in cargo)
            {
                item.InvoiceId = invoice.Id;
                item.ShipmentMovementId = movement.Id;
                item.ShipmentContainerId = container?.Id;
                item.AssignedTransportMode = movement.Mode;
                item.CarrierProvider = container?.Provider ?? movement.CarrierName;
                item.ExternalTrackingNumber = container?.ContainerNumber ??
                    movement.FlightNumber ?? movement.MovementCode;
                var allocation = new CargoAllocation
                {
                    CargoItemId = item.Id,
                    InvoiceId = invoice.Id,
                    ShipmentMovementId = movement.Id,
                    ShipmentContainerId = container?.Id,
                    Mode = movement.Mode,
                    ContainerType = container?.Type,
                    CarrierName = movement.CarrierName,
                    AssignedByUserId = actorUserId.ToString(),
                    AssignedAt = now,
                    Reason = command.Reason?.Trim()
                };
                allocations.Add(allocation);
                _context.CargoAllocations.Add(allocation);
            }
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CargoAllocationResult(movement.Id, allocations.Count, allocations);
        });
    }

    public async Task UnassignAsync(Guid cargoId, string reason, Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An unassignment reason is required.");
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable,
                cancellationToken);
            var cargo = await _context.CargoItems.FirstOrDefaultAsync(x => x.Id == cargoId, cancellationToken)
                ?? throw new KeyNotFoundException("Cargo item not found.");
            var allocation = await _context.CargoAllocations.FirstOrDefaultAsync(x =>
                x.CargoItemId == cargoId && x.UnassignedAt == null, cancellationToken)
                ?? throw new InvalidOperationException("Cargo item has no active shipment allocation.");
            allocation.UnassignedAt = DateTime.UtcNow;
            allocation.UnassignedByUserId = actorUserId.ToString();
            allocation.Reason = reason.Trim();
            cargo.ShipmentMovementId = null;
            cargo.ShipmentContainerId = null;
            cargo.AssignedTransportMode = null;
            cargo.CarrierProvider = null;
            cargo.ExternalTrackingNumber = null;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
