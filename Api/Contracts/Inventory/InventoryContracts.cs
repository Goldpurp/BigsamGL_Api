using Domain.Enums;

namespace Api.Controllers;

public sealed record CreateInventoryItemRequest(string Sku, string? Barcode, string Name,
    string Description, string Category, InventoryItemType Type, string UnitOfMeasure,
    decimal DefaultUnitCost, string Currency, Guid? PreferredSupplierId, bool IsLotTracked);

public sealed record UpdateInventoryItemRequest(string? Barcode, string Name, string Description,
    string Category, InventoryItemType Type, string UnitOfMeasure, decimal DefaultUnitCost,
    string Currency, Guid? PreferredSupplierId, bool IsLotTracked, bool IsActive);

public sealed record CreateInventoryLocationRequest(Guid BranchId, string Code, string Name,
    InventoryLocationType Type, string AddressOrArea);

public sealed record UpdateInventoryLocationRequest(string Name, InventoryLocationType Type,
    string AddressOrArea, bool IsActive);

public sealed record UpdateStockPolicyRequest(decimal QuantityReserved, decimal ReorderLevel,
    decimal TargetStockLevel);

public sealed record InventoryAdjustmentRequest(Guid InventoryItemId, Guid InventoryLocationId,
    InventoryTransactionType Type, decimal Quantity, decimal? UnitCost, string Reason,
    string IdempotencyKey, DateTime OccurredAt);

public sealed record InventoryTransferRequest(Guid InventoryItemId, Guid FromLocationId,
    Guid ToLocationId, decimal Quantity, string Reason, string IdempotencyKey, DateTime OccurredAt);

public sealed record BasicStockInRequest(Guid InventoryItemId, Guid InventoryLocationId,
    decimal Quantity, decimal? UnitCost, string Reason, string IdempotencyKey, DateTime OccurredAt);

public sealed record BasicStockOutRequest(Guid InventoryItemId, Guid InventoryLocationId,
    decimal Quantity, string Reason, string IdempotencyKey, DateTime OccurredAt);
