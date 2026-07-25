using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Route("api/operations/additional-service-pricing")]
public sealed class AdditionalServicePricingController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    public AdditionalServicePricingController(IApplicationDbContext context) => _context = context;

    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Administrator)},{nameof(UserRole.OperationsManager)}")]
    [HttpPost]
    public async Task<ActionResult<AdditionalServicePrice>> Create(CreateAdditionalServicePriceRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request.Amount, request.Currency, request.BranchCode,
            request.EffectiveFrom, request.EffectiveTo, cancellationToken);
        if (validation.Error is not null) return BadRequest(validation.Error);
        var code = request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("A price code and name are required.");
        if (await _context.AdditionalServicePrices.AnyAsync(x => x.Code == code &&
                x.BranchCode == validation.BranchCode && x.IsActive, cancellationToken))
            return Conflict("An active price with this code already exists for the branch.");
        var price = new AdditionalServicePrice
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category,
            Unit = request.Unit,
            Amount = request.Amount,
            Currency = validation.Currency,
            BranchCode = validation.BranchCode,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo
        };
        _context.AdditionalServicePrices.Add(price);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/additional-service-pricing/{price.Id}", price);
    }

    [Authorize(Policy = "OperationsTeam")]
    [HttpGet]
    public async Task<ActionResult> Get([FromQuery] AdditionalServiceCategory? category,
        [FromQuery] PriceUnit? unit, [FromQuery] string? branchCode, [FromQuery] string? currency,
        [FromQuery] bool includeInactive = false, [FromQuery] DateTime? effectiveAt = null,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.AdditionalServicePrices.AsNoTracking().AsQueryable();
        if (category.HasValue) query = query.Where(x => x.Category == category);
        if (unit.HasValue) query = query.Where(x => x.Unit == unit);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var branch = branchCode.Trim().ToUpperInvariant(); query = query.Where(x => x.BranchCode == branch);
        }
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Currency == value);
        }
        if (effectiveAt.HasValue)
            query = query.Where(x => x.EffectiveFrom <= effectiveAt &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveAt));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term) ||
                x.Description.ToLower().Contains(term));
        }
        var ordered = query.OrderBy(x => x.Category).ThenBy(x => x.Name)
            .ThenByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id);
        return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Administrator)},{nameof(UserRole.OperationsManager)}")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdditionalServicePrice>> Replace(Guid id,
        ReplaceAdditionalServicePriceRequest request, CancellationToken cancellationToken)
    {
        var current = await _context.AdditionalServicePrices.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Additional service price was not found.");
        if (!current.IsActive) return Conflict("Only an active price can be replaced.");
        var validation = await ValidateAsync(request.Amount, request.Currency, current.BranchCode,
            request.EffectiveFrom, null, cancellationToken);
        if (validation.Error is not null) return BadRequest(validation.Error);
        if (request.EffectiveFrom <= current.EffectiveFrom)
            return BadRequest("Replacement effective date must be later than the current version.");
        current.IsActive = false;
        current.EffectiveTo = request.EffectiveFrom.AddTicks(-1);
        var replacement = new AdditionalServicePrice
        {
            Code = current.Code,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category,
            Unit = request.Unit,
            Amount = request.Amount,
            Currency = validation.Currency,
            BranchCode = current.BranchCode,
            EffectiveFrom = request.EffectiveFrom,
            ReplacesPriceId = current.Id
        };
        _context.AdditionalServicePrices.Add(replacement);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(replacement);
    }

    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Administrator)},{nameof(UserRole.OperationsManager)}")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var price = await _context.AdditionalServicePrices.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Additional service price was not found.");
        if (!price.IsActive) return NoContent();
        price.IsActive = false;
        price.EffectiveTo ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<(string? Error, string Currency, string BranchCode)> ValidateAsync(decimal amount,
        string currency, string branchCode, DateTime effectiveFrom, DateTime? effectiveTo,
        CancellationToken cancellationToken)
    {
        if (amount < 0) return ("Price cannot be negative.", string.Empty, string.Empty);
        if (effectiveTo.HasValue && effectiveTo <= effectiveFrom)
            return ("Effective-to date must be later than effective-from date.", string.Empty, string.Empty);
        var branch = string.IsNullOrWhiteSpace(branchCode) ? "GLOBAL" : branchCode.Trim().ToUpperInvariant();
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (branch == "GLOBAL")
            return normalizedCurrency.Length == 3
                ? (null, normalizedCurrency, branch)
                : ("A three-letter currency is required.", string.Empty, string.Empty);
        var country = await _context.Branches.AsNoTracking().Where(x => x.Code == branch && x.IsActive)
            .Select(x => x.CountryCode).FirstOrDefaultAsync(cancellationToken);
        if (country is null) return ("Active branch was not found.", string.Empty, string.Empty);
        var localCurrency = CountryCurrency.ForCountry(country, normalizedCurrency);
        if (!string.Equals(localCurrency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
            return ($"{branch} prices must use {localCurrency}.", string.Empty, string.Empty);
        return (null, localCurrency, branch);
    }
}
