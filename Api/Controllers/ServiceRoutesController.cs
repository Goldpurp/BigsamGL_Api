using Api.Exceptions;
using Api.Services;
using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.ServiceRoutes;

/// <summary>API-owned hub-to-destination routes used by customer shipments, manifests and prices.</summary>
[ApiController]
[Authorize(Policy = "StaffOnly")]
[Route("api/service-routes")]
public sealed class ServiceRoutesController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;

    public ServiceRoutesController(ApplicationDbContext context, IActiveBranchContext branches)
    {
        _context = context;
        _branches = branches;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] CustomerShipmentMethod? method,
        [FromQuery] string? destinationCountryCode, [FromQuery] bool includeArchived = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.ServiceRoutes.AsNoTracking().Where(x =>
            (branch.IsAllBranches || x.OriginBranchId == branch.BranchId) &&
            (includeArchived || x.IsActive));
        if (method.HasValue) query = query.Where(x => x.Mode == Mode(method.Value));
        if (!string.IsNullOrWhiteSpace(destinationCountryCode))
        {
            var country = CountryCode(destinationCountryCode);
            query = query.Where(x => x.DestinationCountryCode == country);
        }
        var result = await query.OrderBy(x => x.Mode).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .ToPagedResponseAsync(page, Math.Clamp(pageSize, 1, 200), true, cancellationToken);
        var items = await MapMany(result.Items, cancellationToken);
        return Ok(new PagedResponse<ServiceRouteResponse>(items, result.Page, result.PageSize,
            result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ServiceRouteResponse>> Get(Guid id,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var route = await _context.ServiceRoutes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (branch.IsAllBranches || x.OriginBranchId == branch.BranchId), cancellationToken)
            ?? throw new KeyNotFoundException("Service route not found.");
        Response.Headers.ETag = Quote(route.RowVersion);
        return Ok((await MapMany([route], cancellationToken)).Single());
    }

    [HttpPost]
    public async Task<ActionResult<ServiceRouteResponse>> Create(SaveServiceRouteRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var key = RequireIdempotencyKey();
        Validate(request, branch);
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "service-route.create" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null)
            return Ok(await MapOne(prior.ResourceId, cancellationToken));
        var code = request.ServiceCode.Trim().ToUpperInvariant();
        if (await _context.ServiceRoutes.AnyAsync(x => x.Code == code, cancellationToken))
            throw new ApiProblemException(409, "SERVICE_CODE_IN_USE", "Service code is already in use",
                "Choose a unique route service code.");
        var destinationCountry = CountryCode(request.DestinationCountryCode);
        var route = new ServiceRoute
        {
            Code = code,
            Name = request.DisplayName.Trim(),
            OriginBranchId = branch.BranchId!.Value,
            DestinationBranchId = await DestinationBranch(destinationCountry,
                request.DestinationCity, cancellationToken),
            DestinationCountryCode = destinationCountry,
            DestinationCity = request.DestinationCity.Trim(),
            DestinationRegion = Clean(request.DestinationRegion),
            EstimatedTimeline = request.EstimatedTimeline.Trim(),
            Mode = Mode(request.Method),
            TransitDaysMin = 1,
            TransitDaysMax = 1,
            VolumetricDivisor = request.Method == CustomerShipmentMethod.Air ? 5000 : 1000000,
            IsActive = true
        };
        _context.ServiceRoutes.Add(route);
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            ActorUserId = CurrentUserId, Operation = "service-route.create", Key = key,
            ResourceType = nameof(ServiceRoute), ResourceId = route.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapOne(route.Id, cancellationToken);
        Response.Headers.ETag = Quote(route.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = route.Id }, response);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ServiceRouteResponse>> Update(Guid id,
        SaveServiceRouteRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        Validate(request, branch);
        var route = await _context.ServiceRoutes.FirstOrDefaultAsync(x => x.Id == id &&
            x.OriginBranchId == branch.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException("Service route not found.");
        CheckVersion(route.RowVersion, version);
        var code = request.ServiceCode.Trim().ToUpperInvariant();
        if (await _context.ServiceRoutes.AsNoTracking().AnyAsync(x => x.Id != id && x.Code == code,
            cancellationToken))
            throw new ApiProblemException(409, "SERVICE_CODE_IN_USE", "Service code is already in use",
                "Choose a unique route service code.");
        var linkedMethod = request.Method == CustomerShipmentMethod.Air
            ? ShippingPriceServiceMethod.Air : ShippingPriceServiceMethod.Sea;
        if (await _context.ShippingPriceCatalogueItems.AsNoTracking().AnyAsync(x =>
            x.ServiceRouteId == route.Id && (x.BranchId != branch.BranchId || x.Method != linkedMethod),
            cancellationToken))
            throw new ApiProblemException(409, "ROUTE_HAS_INCOMPATIBLE_PRICES",
                "Linked prices would no longer match this route",
                "Archive this route and create a new Air/Sea route rather than changing its origin or method.");
        var destinationCountry = CountryCode(request.DestinationCountryCode);
        route.Code = code;
        route.Name = request.DisplayName.Trim();
        route.DestinationBranchId = await DestinationBranch(destinationCountry,
            request.DestinationCity, cancellationToken);
        route.DestinationCountryCode = destinationCountry;
        route.DestinationCity = request.DestinationCity.Trim();
        route.DestinationRegion = Clean(request.DestinationRegion);
        route.EstimatedTimeline = request.EstimatedTimeline.Trim();
        route.Mode = Mode(request.Method);
        route.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapOne(route.Id, cancellationToken);
        Response.Headers.ETag = Quote(route.RowVersion);
        return Ok(response);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<ServiceRouteResponse>> Archive(Guid id,
        ArchiveServiceRouteRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("ARCHIVE_REASON_REQUIRED", "A route archive reason is required.");
        var route = await _context.ServiceRoutes.FirstOrDefaultAsync(x => x.Id == id &&
            x.OriginBranchId == branch.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException("Service route not found.");
        CheckVersion(route.RowVersion, version);
        route.IsActive = false;
        route.RowVersion++;
        var linked = await _context.ShippingPriceCatalogueItems.Where(x =>
            x.ServiceRouteId == route.Id && x.State == ShippingPriceState.Active).ToListAsync(cancellationToken);
        foreach (var price in linked)
        {
            price.State = ShippingPriceState.Inactive;
            price.RowVersion++;
        }
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapOne(route.Id, cancellationToken);
        Response.Headers.ETag = Quote(route.RowVersion);
        return Ok(response);
    }

    private async Task<ServiceRouteResponse> MapOne(Guid id, CancellationToken cancellationToken)
    {
        var route = await _context.ServiceRoutes.AsNoTracking().SingleAsync(x => x.Id == id,
            cancellationToken);
        return (await MapMany([route], cancellationToken)).Single();
    }

    private async Task<IReadOnlyList<ServiceRouteResponse>> MapMany(
        IReadOnlyList<ServiceRoute> routes, CancellationToken cancellationToken)
    {
        var branchIds = routes.Select(x => x.OriginBranchId).Distinct().ToList();
        var branches = await _context.Branches.AsNoTracking().Where(x => branchIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return routes.Select(x =>
        {
            var branch = branches[x.OriginBranchId];
            return new ServiceRouteResponse(x.Id, x.RowVersion, x.Code, x.OriginBranchId,
                branch.Code, branch.Name, HubCity(branch.Code, branch.Name), branch.CountryCode,
                x.DestinationCountryCode, x.DestinationCity, x.DestinationRegion,
                x.Mode == TransportMode.Sea ? CustomerShipmentMethod.Sea : CustomerShipmentMethod.Air,
                x.Name, x.EstimatedTimeline, x.IsActive, x.CreatedAt, x.LastModifiedAt);
        }).ToList();
    }

    private async Task<Guid?> DestinationBranch(string countryCode, string city,
        CancellationToken cancellationToken)
    {
        var cityKey = CityKey(city);
        return await _context.Branches.AsNoTracking().Where(x => x.IsActive &&
                x.CountryCode == countryCode &&
                (x.Code == "UK-MAN" && cityKey.Contains("MANCHESTER") ||
                 x.Code == "NG-LOS" && cityKey.Contains("LAGOS") ||
                 x.Code == "CN-GZ" && cityKey.Contains("GUANGZHOU")))
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
    }

    private static void Validate(SaveServiceRouteRequest request, ActiveBranchScope branch)
    {
        if (request.OriginHubId == Guid.Empty || request.OriginHubId != branch.BranchId ||
            string.IsNullOrWhiteSpace(request.ServiceCode) ||
            string.IsNullOrWhiteSpace(request.DestinationCountryCode) ||
            string.IsNullOrWhiteSpace(request.DestinationCity) ||
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.EstimatedTimeline) ||
            string.IsNullOrWhiteSpace(request.Reason) || !Enum.IsDefined(request.Method))
            throw Rule("SERVICE_ROUTE_DETAILS_REQUIRED",
                "Service code, selected origin hub, Air/Sea method, destination, timeline and reason are required.");
        var destinationCountry = CountryCode(request.DestinationCountryCode);
        if (destinationCountry == CountryCode(branch.CountryCode) &&
            CityKey(request.DestinationCity) == CityKey(branch.City ?? branch.Name ?? string.Empty))
            throw Rule("DESTINATION_EQUALS_ORIGIN", "A service route cannot end at its origin hub.");
    }

    private Guid RequireIdempotencyKey() =>
        Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key) ? key :
            throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency key required",
                "Send a UUID Idempotency-Key header.");

    private long RequireVersion()
    {
        var raw = Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        return long.TryParse(raw, out var value) ? value :
            throw new ApiProblemException(428, "IF_MATCH_REQUIRED", "Version required",
                "Send the current row version in If-Match.");
    }

    private static void CheckVersion(long current, long supplied)
    {
        if (current != supplied)
            throw new ApiProblemException(412, "VERSION_MISMATCH", "The route changed",
                "Reload the current route before saving again.");
    }

    private static TransportMode Mode(CustomerShipmentMethod method) =>
        method == CustomerShipmentMethod.Air ? TransportMode.Air : TransportMode.Sea;
    private static string CountryCode(string? value)
    {
        var normalized = new string((value ?? string.Empty).Trim().ToUpperInvariant()
            .Where(char.IsLetter).ToArray());
        return normalized switch
        {
            "NIGERIA" or "NGA" => "NG",
            "UNITEDKINGDOM" or "GREATBRITAIN" or "UK" or "GBR" => "GB",
            "CHINA" or "CHN" => "CN",
            "UNITEDSTATES" or "UNITEDSTATESOFAMERICA" or "USA" => "US",
            "CANADA" or "CAN" => "CA",
            _ when normalized.Length is 2 or 3 => normalized,
            _ => normalized[..Math.Min(5, normalized.Length)]
        };
    }
    private static string HubCity(string code, string fallback) => code switch
    {
        "UK-MAN" => "Manchester", "NG-LOS" => "Lagos", "CN-GZ" => "Guangzhou", _ => fallback
    };
    private static string CityKey(string value) => new(value.Trim().ToUpperInvariant()
        .Where(char.IsLetterOrDigit).ToArray());
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Quote(long value) => $"\"{value}\"";
    private static ApiProblemException Rule(string code, string detail) =>
        new(422, code, "Service route rule was not satisfied", detail);
}
