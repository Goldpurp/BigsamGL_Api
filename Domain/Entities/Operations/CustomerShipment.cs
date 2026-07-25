using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>One customer shipment and its primary sales invoice, created atomically.</summary>
public sealed class CustomerShipment : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceNumericFragment { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public CustomerShipmentMethod Method { get; set; }
    public string OriginCountryCode { get; set; } = string.Empty;
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCountryCode { get; set; } = string.Empty;
    public string DestinationCityOrState { get; set; } = string.Empty;
    public string? DeliveryAddress { get; set; }
    public ShipmentHandoverMethod HandoverMethod { get; set; }
    public ShipmentGoodsType GoodsType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Contents { get; set; }
    public int PieceCount { get; set; }
    public CustomerShipmentPricingMethod PricingMethod { get; set; }
    public string? ServiceCodeSnapshot { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public decimal? ChargeableWeightKg { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public string? NegotiatedReason { get; set; }
    public CustomerShipmentMovementState MovementState { get; set; } = CustomerShipmentMovementState.New;
    public Guid? ReferredByClientId { get; set; }
    public string? CustomerNote { get; set; }
    public long RowVersion { get; set; } = 1;
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>Immutable price evidence copied from the selected catalogue or negotiated agreement.</summary>
public sealed class CustomerShipmentPriceComponent : BaseEntity
{
    public Guid CustomerShipmentId { get; set; }
    public Guid? CataloguePriceId { get; set; }
    public ShipmentPriceComponentType Type { get; set; }
    public string CodeSnapshot { get; set; } = string.Empty;
    public string DescriptionSnapshot { get; set; } = string.Empty;
    public string UnitSnapshot { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }
    public int SortOrder { get; set; }
}
