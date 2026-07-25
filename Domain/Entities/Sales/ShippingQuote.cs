using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class ShippingQuote : BaseEntity
{
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid ServiceRouteId { get; set; }
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public string CargoCategory { get; set; } = "General Cargo";
    public decimal ActualWeightKg { get; set; }
    public decimal VolumetricWeightKg { get; set; }
    public decimal BillableWeightKg { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal FuelSurchargeAmount { get; set; }
    public decimal CustomsAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpiresAt { get; set; }
    public Guid? SalesLeadId { get; set; }
}
