using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class CompanyPurchase : BaseEntity
{
    public string PurchaseNumber { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public CompanyPurchaseCategory Category { get; set; }
    public CompanyPurchaseType Type { get; set; } = CompanyPurchaseType.Service;
    public CompanyPurchaseStatus Status { get; set; } = CompanyPurchaseStatus.Draft;
    public PurchaseReceivingStatus ReceivingStatus { get; set; } = PurchaseReceivingStatus.NotRequired;
    public string BranchCode { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SupplierReference { get; set; } = string.Empty;
    public Guid RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? DecisionReason { get; set; }
    public decimal BalanceDue => Math.Max(0, TotalAmount - AmountPaid);
}
