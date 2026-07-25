using Api.Controllers;
using Application.Common;
using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Finance;

[ApiController]
[Authorize(Policy = "StaffOnly")]
[Route("api/finance/currency")]
public sealed class CurrencyController : ControllerBase
{
    private readonly ICurrencyExchangeService _exchange;
    private readonly IApplicationDbContext _context;
    public CurrencyController(ICurrencyExchangeService exchange, IApplicationDbContext context)
    {
        _exchange = exchange; _context = context;
    }

    [HttpGet("rates/{baseCurrency}/{quoteCurrency}")]
    public async Task<ActionResult<ExchangeRateResult>> Rate(string baseCurrency, string quoteCurrency,
        CancellationToken cancellationToken) => Ok(await _exchange.GetLatestRateAsync(baseCurrency, quoteCurrency, cancellationToken));

    [HttpPost("convert")]
    public async Task<ActionResult<CurrencyConversion>> Convert(ConvertCurrencyRequest request,
        CancellationToken cancellationToken) => Ok(await _exchange.ConvertAsync(request.Amount,
            request.SourceCurrency, request.TargetCurrency, cancellationToken));

    [HttpGet("country/{countryCode}")]
    public ActionResult CountryCurrencyCode(string countryCode) => Ok(new
    {
        CountryCode = countryCode.Trim().ToUpperInvariant(), Currency = CountryCurrency.ForCountry(countryCode)
    });

    [HttpGet("history")]
    public async Task<ActionResult> History([FromQuery] string? baseCurrency, [FromQuery] string? quoteCurrency,
        [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var query = _context.ExchangeRateSnapshots.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(baseCurrency)) query = query.Where(x => x.BaseCurrency == baseCurrency.Trim().ToUpper());
        if (!string.IsNullOrWhiteSpace(quoteCurrency)) query = query.Where(x => x.QuoteCurrency == quoteCurrency.Trim().ToUpper());
        return Ok(await query.OrderByDescending(x => x.FetchedAt).Take(Math.Clamp(take, 1, 500)).ToListAsync(cancellationToken));
    }
}
