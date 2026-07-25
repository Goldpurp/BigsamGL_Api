using System.Text.Json;
using Api.Exceptions;
using Api.Services;
using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Sales;

[ApiController]
[Authorize(Policy = "StaffOnly")]
[Route("api/sales/shipping-prices")]
[Route("api/pricing")]
public sealed class ShippingPricesController : ApiControllerBase
{
    private static readonly HashSet<string> Units = new(StringComparer.OrdinalIgnoreCase)
        { "kg", "cbm", "box", "item", "pallet", "vehicle", "shipment", "service", "surcharge", "custom" };
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;

    public ShippingPricesController(ApplicationDbContext context, IActiveBranchContext branches)
    { _context = context; _branches = branches; }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search, [FromQuery] string? category,
        [FromQuery] string? group, [FromQuery] ShippingPriceServiceMethod? method,
        [FromQuery] ShippingPriceState? state, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.ShippingPriceCatalogueItems.AsNoTracking().Where(x =>
            branch.IsAllBranches || x.BranchId == branch.BranchId);
        if (!string.IsNullOrWhiteSpace(category))
        { var value = category.Trim().ToLowerInvariant(); query = query.Where(x => x.Category.ToLower() == value); }
        if (!string.IsNullOrWhiteSpace(group))
        { var value = group.Trim().ToLowerInvariant(); query = query.Where(x => x.Group.ToLower() == value); }
        if (method.HasValue) query = query.Where(x => x.Method == method);
        if (state.HasValue) query = query.Where(x => x.State == state);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Code.ToLower().Contains(value) || x.Name.ToLower().Contains(value) ||
                x.Description.ToLower().Contains(value) || x.ServiceArea.ToLower().Contains(value));
        }
        var result = await query.OrderBy(x => x.Method).ThenBy(x => x.Category).ThenBy(x => x.Group)
            .ThenBy(x => x.Name).ThenBy(x => x.Id).ToPagedResponseAsync(page, pageSize, true, cancellationToken);
        var branchIds = result.Items.Select(x => x.BranchId).Distinct().ToList();
        var branchCodes = await _context.Branches.AsNoTracking().Where(x => branchIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        return Ok(new PagedResponse<ShippingPriceResponse>(result.Items.Select(x => Map(x,
                BranchKey(branchCodes.GetValueOrDefault(x.BranchId)))).ToList(), result.Page,
            result.PageSize, result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
    }

    [HttpGet("categories")]
    public async Task<ActionResult> Categories(CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var values = await _context.ShippingPriceCatalogueItems.AsNoTracking().Where(x =>
                branch.IsAllBranches || x.BranchId == branch.BranchId)
            .GroupBy(x => new { x.Category, x.Group }).Select(x => new
            { x.Key.Category, x.Key.Group, count = x.Count() }).OrderBy(x => x.Category).ThenBy(x => x.Group)
            .ToListAsync(cancellationToken);
        return Ok(values);
    }

    [HttpPost]
    public async Task<ActionResult<ShippingPriceResponse>> Create(SaveShippingPriceRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var key = RequireIdempotencyKey();
        Validate(request);
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "shipping-price.create" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null) return Ok(Map(await _context.ShippingPriceCatalogueItems.AsNoTracking()
            .SingleAsync(x => x.Id == prior.ResourceId && x.BranchId == branch.BranchId, cancellationToken),
            branch.BranchKey!));
        var item = new ShippingPriceCatalogueItem
        {
            BranchId = branch.BranchId!.Value, Currency = Currency(request.Currency, branch.Currency!),
            Source = ShippingPriceSource.Staff,
            State = ShippingPriceState.Active,
            ServiceRouteId = await ResolveRouteId(request, branch.BranchId.Value, null,
                cancellationToken)
        };
        Apply(item, request);
        if (await _context.ShippingPriceCatalogueItems.AnyAsync(x => x.NormalizedCode == item.NormalizedCode,
            cancellationToken)) throw new ApiProblemException(409, "PRICE_CODE_IN_USE",
                "Shipping price code is already in use", "Choose a unique catalogue code.");
        _context.ShippingPriceCatalogueItems.Add(item);
        _context.ShippingPriceHistories.Add(History(item, request.Reason, 1, branch.BranchKey!));
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "shipping-price.create", Key = key, ResourceType = nameof(ShippingPriceCatalogueItem),
            ResourceId = item.Id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken);
        var response = Map(item, branch.BranchKey!); Response.Headers.ETag = Quote(item.RowVersion);
        return CreatedAtAction(nameof(History), new { id = item.Id }, response);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ShippingPriceResponse>> Update(Guid id, SaveShippingPriceRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion(); Validate(request);
        var item = await _context.ShippingPriceCatalogueItems.FirstOrDefaultAsync(x => x.Id == id &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Shipping price not found.");
        CheckVersion(item.RowVersion, version);
        var nextCode = Normalize(request.Code);
        if (await _context.ShippingPriceCatalogueItems.AsNoTracking().AnyAsync(x => x.Id != id &&
            x.NormalizedCode == nextCode, cancellationToken)) throw new ApiProblemException(409,
                "PRICE_CODE_IN_USE", "Shipping price code is already in use", "Choose a unique catalogue code.");
        item.ServiceRouteId = await ResolveRouteId(request, branch.BranchId!.Value,
            item.ServiceRouteId, cancellationToken);
        Apply(item, request); item.RowVersion++;
        _context.ShippingPriceHistories.Add(History(item, request.Reason, item.RowVersion, branch.BranchKey!));
        await _context.SaveChangesAsync(cancellationToken);
        var response = Map(item, branch.BranchKey!); Response.Headers.ETag = Quote(item.RowVersion); return Ok(response);
    }

    [HttpPost("{id:guid}/activate")]
    public Task<ActionResult<ShippingPriceResponse>> Activate(Guid id, ShippingPriceStateRequest request,
        CancellationToken cancellationToken) => SetState(id, ShippingPriceState.Active, request, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    public Task<ActionResult<ShippingPriceResponse>> Deactivate(Guid id, ShippingPriceStateRequest request,
        CancellationToken cancellationToken) => SetState(id, ShippingPriceState.Inactive, request, cancellationToken);

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult> History(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        if (!await _context.ShippingPriceCatalogueItems.AsNoTracking().AnyAsync(x => x.Id == id &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken))
            throw new KeyNotFoundException("Shipping price not found.");
        return Ok(await _context.ShippingPriceHistories.AsNoTracking().Where(x =>
            x.ShippingPriceCatalogueItemId == id).OrderByDescending(x => x.Version)
            .ToPagedResponseAsync(page, pageSize, true, cancellationToken));
    }

    private async Task<ActionResult<ShippingPriceResponse>> SetState(Guid id, ShippingPriceState state,
        ShippingPriceStateRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var version = RequireVersion();
        if (string.IsNullOrWhiteSpace(request.Reason)) throw Rule("CHANGE_REASON_REQUIRED", "A reason is required.");
        var item = await _context.ShippingPriceCatalogueItems.FirstOrDefaultAsync(x => x.Id == id &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Shipping price not found.");
        CheckVersion(item.RowVersion, version); item.State = state; item.RowVersion++;
        _context.ShippingPriceHistories.Add(History(item, request.Reason, item.RowVersion, branch.BranchKey!));
        await _context.SaveChangesAsync(cancellationToken);
        var response = Map(item, branch.BranchKey!); Response.Headers.ETag = Quote(item.RowVersion); return Ok(response);
    }

    private ShippingPriceHistory History(ShippingPriceCatalogueItem item, string reason, long version,
        string branchKey) => new()
    { ShippingPriceCatalogueItemId = item.Id, Version = version,
      SnapshotJson = JsonSerializer.Serialize(Map(item, branchKey)),
      Reason = reason.Trim(), ChangedByUserId = CurrentUserId };
    private static void Apply(ShippingPriceCatalogueItem item, SaveShippingPriceRequest request)
    {
        item.Code = request.Code.Trim().ToUpperInvariant(); item.NormalizedCode = Normalize(item.Code);
        item.ServiceCode = string.IsNullOrWhiteSpace(request.ServiceCode)
            ? item.Code : request.ServiceCode.Trim().ToUpperInvariant();
        item.Category = request.Category.Trim(); item.Group = request.Group.Trim(); item.Name = request.Name.Trim();
        item.NormalizedName = Normalize(item.Name); item.Description = request.Description.Trim();
        item.ServiceArea = request.ServiceArea.Trim(); item.Method = request.Method;
        item.DestinationCountryCodes = CountryCodes(request.DestinationCountryCodes);
        item.DestinationZone = request.DestinationZone?.Trim() ?? string.Empty;
        item.Unit = request.Unit.Trim().ToLowerInvariant(); item.PriceKind = request.PriceKind;
        item.LowerPrice = request.LowerPrice; item.UpperPrice = request.UpperPrice;
        item.HandlingFee = request.HandlingFee; item.MinimumCharge = request.MinimumCharge;
        item.DocumentationFee = request.DocumentationFee;
        item.MinimumQuantity = request.MinimumQuantity;
        item.MaximumQuantity = request.MaximumQuantity;
        item.DispatchSchedule = NullIfWhiteSpace(request.DispatchSchedule);
        item.EstimatedDelivery = NullIfWhiteSpace(request.EstimatedDelivery);
        item.IsPublic = request.IsPublic;
        item.DisplayOrder = Math.Max(0, request.DisplayOrder);
        if (!string.IsNullOrWhiteSpace(request.Currency))
            item.Currency = Currency(request.Currency, item.Currency);
    }
    private static void Validate(SaveShippingPriceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.Group) || string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.ServiceArea) ||
            string.IsNullOrWhiteSpace(request.Unit) || string.IsNullOrWhiteSpace(request.Reason) ||
            !Units.Contains(request.Unit)) throw Rule("SHIPPING_PRICE_DETAILS_REQUIRED",
                "Code, managed category/group, name, description, service area, supported unit and reason are required.");
        var invalid = request.PriceKind switch
        {
            ShippingPriceKind.CustomQuote => request.LowerPrice.HasValue || request.UpperPrice.HasValue,
            ShippingPriceKind.Fixed or ShippingPriceKind.From => !request.LowerPrice.HasValue || request.LowerPrice < 0,
            ShippingPriceKind.Range => !request.LowerPrice.HasValue || !request.UpperPrice.HasValue ||
                request.LowerPrice < 0 || request.UpperPrice < request.LowerPrice,
            _ => true
        };
        if (invalid || request.HandlingFee < 0 || request.DocumentationFee < 0 ||
            request.MinimumCharge < 0 || request.MinimumQuantity < 0 ||
            request.MaximumQuantity < request.MinimumQuantity ||
            !string.IsNullOrWhiteSpace(request.Currency) &&
            (request.Currency.Trim().Length != 3 || !request.Currency.Trim().All(char.IsLetter)))
            throw Rule("INVALID_SHIPPING_PRICE", "Price values do not match the selected price kind.");
    }
    private static ShippingPriceResponse Map(ShippingPriceCatalogueItem x, string branchKey) => new(x.Id, x.RowVersion,
        x.BranchId, branchKey, x.Currency, x.ServiceCode, x.Code, x.Category, x.Group, x.Name,
        x.Description, x.ServiceArea, x.DestinationCountryCodes, x.DestinationZone, x.Method,
        x.Unit, x.PriceKind, x.LowerPrice, x.UpperPrice, x.HandlingFee, x.DocumentationFee,
        x.MinimumCharge, x.MinimumQuantity, x.MaximumQuantity, x.DispatchSchedule,
        x.EstimatedDelivery, x.Source, x.State, x.IsPublic, x.DisplayOrder,
        x.CreatedAt, x.LastModifiedAt, x.ServiceRouteId);

    private async Task<Guid?> ResolveRouteId(SaveShippingPriceRequest request, Guid branchId,
        Guid? current, CancellationToken cancellationToken)
    {
        var requested = request.ServiceRouteId;
        var serviceCode = string.IsNullOrWhiteSpace(request.ServiceCode)
            ? request.Code.Trim().ToUpperInvariant() : request.ServiceCode.Trim().ToUpperInvariant();
        var route = requested.HasValue
            ? await _context.ServiceRoutes.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == requested && x.OriginBranchId == branchId && x.IsActive, cancellationToken)
            : await _context.ServiceRoutes.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Code == serviceCode && x.OriginBranchId == branchId && x.IsActive, cancellationToken);
        if (requested.HasValue && route is null)
            throw Rule("SERVICE_ROUTE_NOT_FOUND",
                "The selected active service route does not belong to this hub.");
        if (route is not null)
        {
            var expected = request.Method == ShippingPriceServiceMethod.Sea
                ? TransportMode.Sea : TransportMode.Air;
            if (request.Method != ShippingPriceServiceMethod.Other && route.Mode != expected)
                throw Rule("SERVICE_ROUTE_METHOD_MISMATCH",
                    "The shipping price method must match the selected Air or Sea route.");
            return route.Id;
        }
        return current;
    }
    private static string BranchKey(string? code) => code switch
    {
        "UK-MAN" => "manchester", "NG-LOS" => "lagos", "CN-GZ" => "guangzhou",
        _ => code?.ToLowerInvariant() ?? "unknown"
    };
    private Guid RequireIdempotencyKey() => Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key)
        ? key : throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency key required",
            "Send a UUID Idempotency-Key header.");
    private long RequireVersion()
    {
        var raw = Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        return long.TryParse(raw, out var value) ? value : throw new ApiProblemException(428,
            "IF_MATCH_REQUIRED", "Version required", "Send the current row version in If-Match.");
    }
    private static void CheckVersion(long current, long supplied)
    { if (current != supplied) throw new ApiProblemException(412, "VERSION_MISMATCH", "The record changed",
        "Reload the current version before saving again."); }
    private static string Normalize(string value) => string.Join(' ', value.Trim().ToUpperInvariant()
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string Currency(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
    private static string CountryCodes(string? value) => string.Join(',',
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant()).Distinct());
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Quote(long value) => $"\"{value}\"";
    private static ApiProblemException Rule(string code, string detail) =>
        new(422, code, "Shipping price rule was not satisfied", detail);
}
