using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class InventoryItem : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public InventoryItemType Type { get; set; }
    public string UnitOfMeasure { get; set; } = "Each";
    public decimal DefaultUnitCost { get; set; }
    public string Currency { get; set; } = "GBP";
    public Guid? PreferredSupplierId { get; set; }
    public bool IsLotTracked { get; set; }
    public bool IsActive { get; set; } = true;
}
