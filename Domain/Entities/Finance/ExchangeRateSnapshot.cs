using Domain.Common;

namespace Domain.Entities;

public sealed class ExchangeRateSnapshot : BaseEntity
{
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTime EffectiveAt { get; set; }
    public DateTime FetchedAt { get; set; }
    public bool IsFallback { get; set; }
    public string? ProviderReference { get; set; }
}
