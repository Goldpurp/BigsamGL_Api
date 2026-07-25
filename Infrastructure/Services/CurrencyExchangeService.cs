using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public sealed class CurrencyExchangeService : ICurrencyExchangeService
{
    private readonly HttpClient _httpClient;
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public CurrencyExchangeService(HttpClient httpClient, IApplicationDbContext context, IConfiguration configuration)
    {
        _httpClient = httpClient; _context = context; _configuration = configuration;
    }

    public async Task<ExchangeRateResult> GetLatestRateAsync(string baseCurrency, string quoteCurrency,
        CancellationToken cancellationToken)
    {
        var source = Normalize(baseCurrency); var target = Normalize(quoteCurrency);
        if (source == target) return new ExchangeRateResult(source, target, 1, "Identity", DateTime.UtcNow,
            DateTime.UtcNow, false, null);
        var cacheMinutes = Math.Clamp(_configuration.GetValue("ExchangeRates:CacheMinutes", 30), 1, 1440);
        var allowConfiguredFallback = _configuration.GetValue("ExchangeRates:AllowConfiguredFallback", false);
        var freshAfter = DateTime.UtcNow.AddMinutes(-cacheMinutes);
        var cached = await _context.ExchangeRateSnapshots.AsNoTracking().Where(x =>
                x.BaseCurrency == source && x.QuoteCurrency == target && x.FetchedAt >= freshAfter &&
                (!x.IsFallback || allowConfiguredFallback))
            .OrderByDescending(x => x.FetchedAt).FirstOrDefaultAsync(cancellationToken);
        if (cached is not null) return Map(cached);

        try
        {
            var baseUrl = (_configuration["ExchangeRates:BaseUrl"] ?? "https://api.frankfurter.dev").TrimEnd('/');
            var provider = target == "NGN" || source == "NGN" ? "CBN" : _configuration["ExchangeRates:DefaultProvider"];
            var uri = $"{baseUrl}/v2/rate/{Uri.EscapeDataString(source)}/{Uri.EscapeDataString(target)}" +
                (string.IsNullOrWhiteSpace(provider) ? string.Empty : $"?providers={Uri.EscapeDataString(provider)}");
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<FrankfurterRate>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Exchange-rate provider returned an empty response.");
            if (payload.Rate <= 0) throw new InvalidOperationException("Exchange-rate provider returned an invalid rate.");
            var now = DateTime.UtcNow;
            var snapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = source, QuoteCurrency = target, Rate = payload.Rate,
                Provider = payload.Provider ?? provider ?? "Frankfurter",
                EffectiveAt = ParseEffectiveDate(payload.Date, now), FetchedAt = now,
                ProviderReference = uri, IsFallback = false
            };
            _context.ExchangeRateSnapshots.Add(snapshot);
            await _context.SaveChangesAsync(cancellationToken);
            return Map(snapshot);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            var lastKnown = await _context.ExchangeRateSnapshots.AsNoTracking().Where(x =>
                    x.BaseCurrency == source && x.QuoteCurrency == target && !x.IsFallback)
                .OrderByDescending(x => x.EffectiveAt).FirstOrDefaultAsync(cancellationToken);
            var maxStaleHours = Math.Clamp(_configuration.GetValue("ExchangeRates:MaxStaleHours", 72), 1, 720);
            if (lastKnown is not null && lastKnown.EffectiveAt >= DateTime.UtcNow.AddHours(-maxStaleHours))
                return new ExchangeRateResult(lastKnown.BaseCurrency, lastKnown.QuoteCurrency, lastKnown.Rate,
                    $"{lastKnown.Provider} (last known)", lastKnown.EffectiveAt, lastKnown.FetchedAt, true, lastKnown.Id);
            if (!allowConfiguredFallback)
                throw new InvalidOperationException($"A current {source}/{target} exchange rate is unavailable.");
            var fallback = _configuration.GetValue<decimal?>($"ExchangeRates:FallbackRates:{source}_{target}");
            if (!fallback.HasValue || fallback <= 0)
                throw new InvalidOperationException($"A current {source}/{target} exchange rate is unavailable.");
            var fallbackSnapshot = new ExchangeRateSnapshot
            {
                BaseCurrency = source, QuoteCurrency = target, Rate = fallback.Value,
                Provider = "Configured development fallback", EffectiveAt = DateTime.UtcNow,
                FetchedAt = DateTime.UtcNow, ProviderReference = "configuration", IsFallback = true
            };
            _context.ExchangeRateSnapshots.Add(fallbackSnapshot);
            await _context.SaveChangesAsync(cancellationToken);
            return Map(fallbackSnapshot);
        }
    }

    public async Task<CurrencyConversion> ConvertAsync(decimal amount, string sourceCurrency,
        string targetCurrency, CancellationToken cancellationToken)
    {
        if (amount < 0) throw new ArgumentException("Currency conversion amount cannot be negative.");
        var rate = await GetLatestRateAsync(sourceCurrency, targetCurrency, cancellationToken);
        return new CurrencyConversion(amount, rate.BaseCurrency, Math.Round(amount * rate.Rate, 2),
            rate.QuoteCurrency, rate.Rate, rate.Provider, rate.EffectiveAt, rate.FetchedAt,
            rate.IsFallback, rate.SnapshotId);
    }

    private static ExchangeRateResult Map(ExchangeRateSnapshot value) => new(value.BaseCurrency,
        value.QuoteCurrency, value.Rate, value.Provider, value.EffectiveAt, value.FetchedAt,
        value.IsFallback, value.Id);

    private static string Normalize(string currency)
    {
        var value = currency?.Trim().ToUpperInvariant();
        if (value is null || value.Length != 3 || !value.All(char.IsLetter))
            throw new ArgumentException("Currency must be a three-letter ISO code.");
        return value;
    }

    private static DateTime ParseEffectiveDate(string? value, DateTime fallback) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime() : fallback;

    private sealed record FrankfurterRate(string? Date, string? Base, string? Quote, decimal Rate, string? Provider);
}
