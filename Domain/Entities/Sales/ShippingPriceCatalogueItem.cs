using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class ShippingPriceCatalogueItem : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid? ServiceRouteId { get; set; }
    /// <summary>Groups independently editable price rows into one public cargo service.</summary>
    public string ServiceCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ServiceArea { get; set; } = string.Empty;
    /// <summary>Comma-separated ISO country codes served by this price row.</summary>
    public string DestinationCountryCodes { get; set; } = string.Empty;
    public string DestinationZone { get; set; } = string.Empty;
    public ShippingPriceServiceMethod Method { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public ShippingPriceKind PriceKind { get; set; }
    public decimal? LowerPrice { get; set; }
    public decimal? UpperPrice { get; set; }
    public decimal? HandlingFee { get; set; }
    public decimal? DocumentationFee { get; set; }
    public decimal? MinimumCharge { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public decimal? MaximumQuantity { get; set; }
    public string? DispatchSchedule { get; set; }
    public string? EstimatedDelivery { get; set; }
    public bool IsPublic { get; set; } = true;
    public int DisplayOrder { get; set; }
    public ShippingPriceSource Source { get; set; }
    public ShippingPriceState State { get; set; } = ShippingPriceState.Active;
    public long RowVersion { get; set; } = 1;
}

public sealed class ShippingPriceHistory : BaseEntity
{
    public Guid ShippingPriceCatalogueItemId { get; set; }
    public long Version { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string Reason { get; set; } = string.Empty;
    public Guid ChangedByUserId { get; set; }
}
