using Domain.Common;

namespace Domain.Entities;

public class RateCard : BaseEntity
{
    public Guid ServiceRouteId { get; set; }
    public string CargoCategory { get; set; } = "General Cargo";
    public decimal MinimumWeightKg { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal MinimumCharge { get; set; }
    public decimal FuelSurchargeRate { get; set; }
    public decimal CustomsRate { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
