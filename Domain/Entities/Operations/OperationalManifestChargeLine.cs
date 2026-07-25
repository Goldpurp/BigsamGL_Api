using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Immutable calculation evidence for one shipment invoice. Air lines retain dimensions and
/// volumetric maths; Sea and negotiated lines retain the exact commercial units and rates.
/// </summary>
public sealed class OperationalManifestChargeLine : BaseEntity
{
    public Guid ManifestEntryId { get; set; }
    public ShipmentChargeLineType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? BoxSize { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public decimal? VolumetricDivisor { get; set; }
    public decimal? VolumetricWeightKg { get; set; }
    public decimal? BillableWeightKg { get; set; }
    public decimal LineAmount { get; set; }
    public int SortOrder { get; set; }
}
