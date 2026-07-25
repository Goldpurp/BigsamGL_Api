using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class AdditionalServicePrice : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AdditionalServiceCategory Category { get; set; }
    public PriceUnit Unit { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public string BranchCode { get; set; } = "GLOBAL";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? ReplacesPriceId { get; set; }
}

