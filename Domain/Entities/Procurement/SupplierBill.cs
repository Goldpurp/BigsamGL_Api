using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class SupplierBill : BaseEntity
{
    public Guid SupplierId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public string SupplierReference { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "GBP";
    public SupplierBillStatus Status { get; set; } = SupplierBillStatus.Draft;
    public string Description { get; set; } = string.Empty;
}
