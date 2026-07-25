using System.ComponentModel.DataAnnotations.Schema;
using Domain.Common;

namespace Domain.Entities;

public sealed class InventoryStockLevel : BaseEntity
{
    public Guid InventoryItemId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal AverageUnitCost { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal TargetStockLevel { get; set; }
    public DateTime? LastMovementAt { get; set; }

    [NotMapped]
    public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;
}
