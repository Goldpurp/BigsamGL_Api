using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Route("api/operations/routes")]
public sealed class RoutesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    public RoutesController(IApplicationDbContext context) => _context = context;

    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Administrator)},{nameof(UserRole.OperationsManager)}")]
    [HttpPost]
    public async Task<ActionResult<ServiceRoute>> Create(CreateServiceRouteRequest request, CancellationToken cancellationToken)
    {
        if (request.OriginBranchId == request.DestinationBranchId || request.TransitDaysMin < 1 || request.TransitDaysMax < request.TransitDaysMin)
            return BadRequest("Route branches and transit-day range are invalid.");
        var branchCount = await _context.Branches.CountAsync(x => x.Id == request.OriginBranchId || x.Id == request.DestinationBranchId, cancellationToken);
        if (branchCount != 2) return BadRequest("Both route branches must exist.");
        var route = new ServiceRoute { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(),
            OriginBranchId = request.OriginBranchId, DestinationBranchId = request.DestinationBranchId, Mode = request.Mode,
            DestinationCountryCode = (await _context.Branches.AsNoTracking()
                .Where(x => x.Id == request.DestinationBranchId).Select(x => x.CountryCode)
                .SingleAsync(cancellationToken)).ToUpperInvariant(),
            DestinationCity = (await _context.Branches.AsNoTracking()
                .Where(x => x.Id == request.DestinationBranchId).Select(x => x.Name)
                .SingleAsync(cancellationToken)),
            EstimatedTimeline = $"{request.TransitDaysMin}–{request.TransitDaysMax} days",
            TransitDaysMin = request.TransitDaysMin, TransitDaysMax = request.TransitDaysMax,
            WeeklyDepartureDay = request.WeeklyDepartureDay, VolumetricDivisor = request.VolumetricDivisor };
        _context.ServiceRoutes.Add(route); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/routes/{route.Id}", route);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult> Get(CancellationToken cancellationToken)
    {
        var routes = await (from route in _context.ServiceRoutes.AsNoTracking()
            join origin in _context.Branches.AsNoTracking() on route.OriginBranchId equals origin.Id
            join destination in _context.Branches.AsNoTracking()
                on route.DestinationBranchId equals (Guid?)destination.Id into destinations
            from destination in destinations.DefaultIfEmpty()
            join originControl in _context.BranchControls.AsNoTracking() on origin.Id equals originControl.BranchId into originControls
            from originControl in originControls.DefaultIfEmpty()
            where route.IsActive orderby route.Name
            select new { route.Id, route.Code, route.Name, route.Mode, route.TransitDaysMin, route.TransitDaysMax,
                route.WeeklyDepartureDay, Origin = origin.Name,
                Destination = destination == null ? route.DestinationCity : destination.Name,
                BookingEnabled = originControl == null || originControl.IsBookingEnabled,
                ReceivingEnabled = originControl == null || originControl.IsReceivingEnabled,
                DispatchEnabled = originControl == null || originControl.IsDispatchEnabled,
                OperationalNotice = originControl == null ? null : originControl.EmergencyNotice }).ToListAsync(cancellationToken);
        return Ok(routes);
    }

    [AllowAnonymous]
    [HttpGet("{routeId:guid}/rates")]
    public async Task<ActionResult> GetCurrentRates(Guid routeId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rates = await _context.RateCards.AsNoTracking().Where(x => x.ServiceRouteId == routeId && x.IsActive &&
            x.EffectiveFrom <= now && (!x.EffectiveTo.HasValue || x.EffectiveTo >= now))
            .OrderBy(x => x.CargoCategory).ToListAsync(cancellationToken);
        return Ok(rates.Select(x => new { x.Id, x.CargoCategory, x.MinimumWeightKg, x.RatePerKg,
            x.MinimumCharge, x.FuelSurchargeRate, x.CustomsRate, x.Currency, x.EffectiveFrom, x.EffectiveTo }));
    }
}
