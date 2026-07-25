namespace Application.Common.Interfaces;

public record ExchangeRateResult(string BaseCurrency, string QuoteCurrency, decimal Rate,
    string Provider, DateTime EffectiveAt, DateTime FetchedAt, bool IsFallback, Guid? SnapshotId);
public record CurrencyConversion(decimal SourceAmount, string SourceCurrency, decimal ConvertedAmount,
    string TargetCurrency, decimal Rate, string Provider, DateTime RateEffectiveAt, DateTime RateFetchedAt,
    bool IsFallback, Guid? RateSnapshotId);

public interface ICurrencyExchangeService
{
    Task<ExchangeRateResult> GetLatestRateAsync(string baseCurrency, string quoteCurrency,
        CancellationToken cancellationToken);
    Task<CurrencyConversion> ConvertAsync(decimal amount, string sourceCurrency, string targetCurrency,
        CancellationToken cancellationToken);
}
