using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Route("api/operations/rates")]
public sealed class RatesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    public RatesController(IApplicationDbContext context) => _context = context;

    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Administrator)},{nameof(UserRole.OperationsManager)}")]
    [HttpPost]
    public async Task<ActionResult<RateCard>> Create(CreateRateCardRequest request, CancellationToken cancellationToken)
    {
        if (request.RatePerKg < 0 || request.MinimumCharge < 0 || request.MinimumWeightKg < 0 ||
            request.FuelSurchargeRate is < 0 or > 1 || request.CustomsRate is < 0 or > 1) return BadRequest("Rate values are invalid.");
        if (!await _context.ServiceRoutes.AnyAsync(x => x.Id == request.ServiceRouteId, cancellationToken)) return BadRequest("Service route not found.");
        var rate = new RateCard { ServiceRouteId = request.ServiceRouteId, CargoCategory = request.CargoCategory.Trim(),
            MinimumWeightKg = request.MinimumWeightKg, RatePerKg = request.RatePerKg, MinimumCharge = request.MinimumCharge,
            FuelSurchargeRate = request.FuelSurchargeRate, CustomsRate = request.CustomsRate,
            Currency = request.Currency.Trim().ToUpperInvariant(), EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo };
        _context.RateCards.Add(rate); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/rates/{rate.Id}", rate);
    }

    [Authorize(Policy = "OperationsTeam")]
    [HttpGet]
    public async Task<ActionResult> Get([FromQuery] Guid? routeId, [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RateCards.AsNoTracking().AsQueryable();
        if (routeId.HasValue) query = query.Where(x => x.ServiceRouteId == routeId);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.ServiceRouteId).ThenBy(x => x.CargoCategory)
            .ThenByDescending(x => x.EffectiveFrom).ToListAsync(cancellationToken));
    }

    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Administrator)},{nameof(UserRole.OperationsManager)}")]
    [HttpPut("{rateId:guid}")]
    public async Task<ActionResult<RateCard>> Replace(Guid rateId, ReplaceRateCardRequest request, CancellationToken cancellationToken)
    {
        var current = await _context.RateCards.FirstOrDefaultAsync(x => x.Id == rateId, cancellationToken)
            ?? throw new KeyNotFoundException("Rate card not found.");
        if (!current.IsActive) return Conflict("Only an active rate card can be replaced.");
        if (request.EffectiveFrom <= current.EffectiveFrom || request.RatePerKg < 0 || request.MinimumCharge < 0 ||
            request.MinimumWeightKg < 0 || request.FuelSurchargeRate is < 0 or > 1 || request.CustomsRate is < 0 or > 1)
            return BadRequest("Replacement rate or effective date is invalid.");
        current.EffectiveTo = request.EffectiveFrom.AddTicks(-1);
        var replacement = new RateCard { ServiceRouteId = current.ServiceRouteId, CargoCategory = current.CargoCategory,
            MinimumWeightKg = request.MinimumWeightKg, RatePerKg = request.RatePerKg, MinimumCharge = request.MinimumCharge,
            FuelSurchargeRate = request.FuelSurchargeRate, CustomsRate = request.CustomsRate,
            Currency = request.Currency.Trim().ToUpperInvariant(), EffectiveFrom = request.EffectiveFrom, IsActive = true };
        _context.RateCards.Add(replacement); await _context.SaveChangesAsync(cancellationToken);
        return Ok(replacement);
    }

    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Administrator)},{nameof(UserRole.OperationsManager)}")]
    [HttpDelete("{rateId:guid}")]
    public async Task<IActionResult> Deactivate(Guid rateId, CancellationToken cancellationToken)
    {
        var rate = await _context.RateCards.FirstOrDefaultAsync(x => x.Id == rateId, cancellationToken)
            ?? throw new KeyNotFoundException("Rate card not found.");
        rate.IsActive = false; rate.EffectiveTo ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken); return NoContent();
    }
}
