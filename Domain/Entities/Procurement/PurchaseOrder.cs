using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class PurchaseOrder : BaseEntity
{
    public Guid SupplierId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GBP";
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public string Description { get; set; } = string.Empty;
}
