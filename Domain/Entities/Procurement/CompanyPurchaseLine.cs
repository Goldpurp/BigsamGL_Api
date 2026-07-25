using Domain.Common;

namespace Domain.Entities;

public sealed class CompanyPurchaseLine : BaseEntity
{
    public Guid CompanyPurchaseId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}
