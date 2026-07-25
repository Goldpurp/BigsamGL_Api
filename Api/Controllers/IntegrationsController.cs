using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/integrations")]
public sealed class IntegrationsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrencyExchangeService _exchange;
    private readonly IConfiguration _configuration;
    public IntegrationsController(IApplicationDbContext context, ICurrencyExchangeService exchange,
        IConfiguration configuration)
    {
        _context = context;
        _exchange = exchange;
        _configuration = configuration;
    }

    [HttpGet("exchange-rates/latest")]
    public async Task<ActionResult> LatestExchangeRates([FromQuery] string baseCurrency = "GBP",
        CancellationToken cancellationToken = default)
    {
        var source = baseCurrency.Trim().ToUpperInvariant();
        if (source.Length != 3 || !source.All(char.IsLetter))
            return BadRequest("Base currency must be a three-letter ISO code.");
        var currencies = new[] { "GBP", "NGN", "CNY", "USD", "EUR" };
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var updatedAt = DateTime.MinValue;
        var fallback = false;
        foreach (var quote in currencies)
        {
            var value = await _exchange.GetLatestRateAsync(source, quote, cancellationToken);
            rates[quote] = value.Rate;
            providers.Add(value.Provider);
            if (value.FetchedAt > updatedAt) updatedAt = value.FetchedAt;
            fallback |= value.IsFallback;
        }
        return Ok(new
        {
            Base = source,
            Rates = rates,
            UpdatedAt = updatedAt,
            Source = string.Join(", ", providers),
            IsFallback = fallback
        });
    }

    [HttpGet("status")]
    public ActionResult Status()
    {
        var mode = _configuration["Integrations:Mode"] ?? "Disabled";
        var live = mode.Equals("Live", StringComparison.OrdinalIgnoreCase);
        return Ok(new
        {
            Mode = mode,
            Providers = Enum.GetNames<IntegrationKind>().Select(x => new
            {
                Kind = x,
                Provider = live ? "Configured production provider" : "BiGSAM local provider",
                Live = live
            })
        });
    }

    [HttpGet("messages")]
    public async Task<ActionResult> Messages([FromQuery] IntegrationKind? kind, [FromQuery] int take = 100,
        [FromQuery] bool paged = false, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.IntegrationMessages.AsNoTracking().AsQueryable(); if (kind.HasValue) query = query.Where(x => x.Kind == kind);
        var ordered = query.OrderByDescending(x => x.ProcessedAt).ThenByDescending(x => x.Id);
        if (paged) return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
        return Ok(await ordered.Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken));
    }
}
