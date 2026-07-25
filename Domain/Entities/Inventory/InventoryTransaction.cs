using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class InventoryTransaction : BaseEntity
{
    public string TransactionNumber { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public InventoryTransactionType Type { get; set; }
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public Guid? CompanyPurchaseLineId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public Guid PerformedByUserId { get; set; }
}
