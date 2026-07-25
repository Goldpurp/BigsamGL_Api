using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces;

public sealed record InventoryAdjustmentCommand(
    Guid InventoryItemId,
    Guid InventoryLocationId,
    InventoryTransactionType Type,
    decimal Quantity,
    decimal? UnitCost,
    string Reason,
    string IdempotencyKey,
    DateTime OccurredAt,
    Guid PerformedByUserId);

public sealed record InventoryTransferCommand(
    Guid InventoryItemId,
    Guid FromLocationId,
    Guid ToLocationId,
    decimal Quantity,
    string Reason,
    string IdempotencyKey,
    DateTime OccurredAt,
    Guid PerformedByUserId);

public sealed record PurchaseReceiptLineCommand(
    Guid CompanyPurchaseLineId,
    Guid InventoryLocationId,
    decimal Quantity);

public sealed record PurchaseReceiptCommand(
    Guid CompanyPurchaseId,
    string IdempotencyKey,
    DateTime OccurredAt,
    string? Notes,
    Guid PerformedByUserId,
    IReadOnlyList<PurchaseReceiptLineCommand> Lines);

public interface IInventoryService
{
    Task<InventoryTransaction> AdjustAsync(InventoryAdjustmentCommand command,
        CancellationToken cancellationToken);
    Task<InventoryTransaction> TransferAsync(InventoryTransferCommand command,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryTransaction>> ReceivePurchaseAsync(PurchaseReceiptCommand command,
        CancellationToken cancellationToken);
}
