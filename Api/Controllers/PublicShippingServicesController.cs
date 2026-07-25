using Api.Controllers.Public;
using Api.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/shipping-services")]
public sealed class PublicShippingServicesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public PublicShippingServicesController(IApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PublicShippingServiceResponse>>> List(
        [FromQuery] string? originCountryCode, [FromQuery] string? destinationCountryCode,
        [FromQuery] ShippingPriceServiceMethod? method,
        CancellationToken cancellationToken)
    {
        var query = from price in _context.ShippingPriceCatalogueItems.AsNoTracking()
                    join branch in _context.Branches.AsNoTracking() on price.BranchId equals branch.Id
                    where price.State == ShippingPriceState.Active && price.IsPublic && branch.IsActive
                    select new { Price = price, Branch = branch };

        if (!string.IsNullOrWhiteSpace(originCountryCode))
        {
            var origin = originCountryCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.Branch.CountryCode == origin);
        }
        if (method.HasValue) query = query.Where(x => x.Price.Method == method);

        var rows = await query.OrderBy(x => x.Branch.Code).ThenBy(x => x.Price.ServiceCode)
            .ThenBy(x => x.Price.DisplayOrder).ThenBy(x => x.Price.Name)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(destinationCountryCode))
        {
            var destination = destinationCountryCode.Trim().ToUpperInvariant();
            rows = rows.Where(x => CountryCodes(x.Price).Contains(destination)).ToList();
        }

        var result = rows.GroupBy(x => new
            {
                x.Price.ServiceCode,
                x.Branch.Id,
                BranchCode = x.Branch.Code,
                BranchName = x.Branch.Name,
                x.Branch.CountryCode,
                x.Price.ServiceArea,
                x.Price.DestinationZone,
                x.Price.Method,
                x.Price.Currency
            })
            .Select(group =>
            {
                var first = group.First().Price;
                return new PublicShippingServiceResponse(group.Key.ServiceCode, group.Key.Id,
                    group.Key.BranchCode, group.Key.BranchName, group.Key.CountryCode,
                    group.Key.ServiceArea, group.Key.DestinationZone,
                    group.SelectMany(x => CountryCodes(x.Price)).Distinct().OrderBy(x => x).ToList(),
                    group.Key.Method, group.Key.Currency, first.DispatchSchedule,
                    first.EstimatedDelivery, group.Select(x => Map(x.Price)).ToList());
            })
            .OrderBy(x => x.OriginHubCode).ThenBy(x => x.Method).ThenBy(x => x.ServiceArea)
            .ToList();

        Response.Headers.CacheControl = "public,max-age=60";
        return Ok(result);
    }

    [HttpPost("estimate")]
    public async Task<ActionResult<PublicShippingEstimateResponse>> Estimate(
        PublicShippingEstimateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceCode) || request.ActualWeightKg < 0)
            throw Rule("ESTIMATE_DETAILS_REQUIRED", "Choose a service and enter a valid actual weight.");

        var serviceCode = request.ServiceCode.Trim().ToUpperInvariant();
        var prices = await _context.ShippingPriceCatalogueItems.AsNoTracking()
            .Where(x => x.ServiceCode == serviceCode && x.State == ShippingPriceState.Active && x.IsPublic)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        if (prices.Count == 0) throw new KeyNotFoundException("Published cargo service was not found.");
        if (prices.Select(x => x.Currency).Distinct().Count() != 1)
            throw Rule("SERVICE_CURRENCY_MISMATCH", "This service contains prices in different currencies.");

        var length = request.LengthCm ?? 0;
        var width = request.WidthCm ?? 0;
        var height = request.HeightCm ?? 0;
        var hasDimensions = length > 0 && width > 0 && height > 0;
        var volumeCbm = hasDimensions ? decimal.Round(length * width * height / 1_000_000m, 4) : (decimal?)null;
        var volumetric = hasDimensions ? decimal.Round(length * width * height / 5_000m, 2) : (decimal?)null;
        var basePrices = prices.Where(IsBase).ToList();
        var usesCbm = basePrices.Any(x => x.Unit.Equals("cbm", StringComparison.OrdinalIgnoreCase));
        var quantity = usesCbm
            ? volumeCbm ?? throw Rule("DIMENSIONS_REQUIRED", "Enter dimensions to calculate a CBM service.")
            : Math.Max(request.ActualWeightKg, volumetric ?? 0);

        var selected = request.Selections ?? [];
        if (selected.GroupBy(x => x.Code.Trim().ToUpperInvariant()).Any(x => x.Count() > 1) ||
            selected.Any(x => x.Quantity <= 0))
            throw Rule("INVALID_PRICE_SELECTION", "Choose each additional price once with a positive quantity.");
        if (basePrices.Count == 0 && selected.Count == 0)
            throw Rule("PRICE_SELECTION_REQUIRED", "Select at least one box, item or vehicle price.");

        var basePrice = basePrices.FirstOrDefault(x => InBand(x, quantity));
        if (basePrice is null && basePrices.Count > 0)
        {
            var firstBand = basePrices.OrderBy(x => x.MinimumQuantity ?? 0).First();
            if (quantity < (firstBand.MinimumQuantity ?? 0)) basePrice = firstBand;
        }
        if (basePrices.Count > 0 && basePrice is null)
            throw Rule("NO_PRICE_BAND", "No published price band covers this shipment quantity.");

        var billable = basePrice is null ? quantity :
            Math.Max(quantity, basePrice.MinimumQuantity ?? 0);
        var lines = new List<PublicShippingEstimateLine>();
        if (basePrice is not null)
        {
            var baseAmount = Calculate(basePrice, billable);
            if (basePrice.MinimumCharge.HasValue)
                baseAmount = Math.Max(baseAmount, basePrice.MinimumCharge.Value);
            lines.Add(Line(basePrice, billable, baseAmount));
        }

        foreach (var selection in selected)
        {
            var code = selection.Code.Trim().ToUpperInvariant();
            var item = prices.FirstOrDefault(x => x.Code == code && !IsBase(x))
                ?? throw Rule("PRICE_NOT_AVAILABLE", $"Price item {code} is not available for this service.");
            if (item.PriceKind == ShippingPriceKind.CustomQuote || !item.LowerPrice.HasValue)
                throw Rule("CUSTOM_QUOTE_REQUIRED", $"{item.Name} requires an agreed staff quotation.");
            lines.Add(Line(item, selection.Quantity, Calculate(item, selection.Quantity)));
        }

        var feeSource = basePrice ?? prices[0];
        var handling = feeSource.HandlingFee ?? 0;
        var documentation = feeSource.DocumentationFee ?? 0;
        if (handling > 0)
            lines.Add(new PublicShippingEstimateLine("HANDLING", "Handling fee", 1, "service",
                handling, handling));
        if (documentation > 0)
            lines.Add(new PublicShippingEstimateLine("DOCUMENTATION", "Documentation fee", 1,
                "service", documentation, documentation));

        return Ok(new PublicShippingEstimateResponse(serviceCode, feeSource.Currency,
            request.ActualWeightKg, volumetric, volumeCbm, billable, usesCbm ? "cbm" : "kg",
            handling, documentation, decimal.Round(lines.Sum(x => x.Amount), 2), lines,
            "Estimate only. Compliance checks, duties, delivery and negotiated cargo may change the final invoice."));
    }

    private static PublicShippingPriceResponse Map(ShippingPriceCatalogueItem x) => new(
        x.Id, x.Code, x.Name, x.Category, x.Group, x.Description, x.Unit, x.PriceKind,
        x.LowerPrice, x.UpperPrice, x.MinimumQuantity, x.MaximumQuantity,
        x.HandlingFee, x.DocumentationFee, x.MinimumCharge, x.DisplayOrder);

    private static IReadOnlyList<string> CountryCodes(ShippingPriceCatalogueItem price) =>
        price.DestinationCountryCodes.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant()).Distinct().ToList();

    private static bool IsBase(ShippingPriceCatalogueItem price) =>
        price.Group.Equals("Base rate", StringComparison.OrdinalIgnoreCase);

    private static bool InBand(ShippingPriceCatalogueItem price, decimal quantity) =>
        quantity >= (price.MinimumQuantity ?? 0) &&
        (!price.MaximumQuantity.HasValue || quantity <= price.MaximumQuantity.Value);

    private static decimal Calculate(ShippingPriceCatalogueItem price, decimal quantity)
    {
        if (!price.LowerPrice.HasValue) return 0;
        var amount = price.Unit is "kg" or "cbm" or "box" or "item" or "pallet" or "vehicle"
            or "surcharge" ? price.LowerPrice.Value * quantity : price.LowerPrice.Value;
        return decimal.Round(amount, 2);
    }

    private static PublicShippingEstimateLine Line(ShippingPriceCatalogueItem price,
        decimal quantity, decimal amount) => new(price.Code, price.Name, quantity,
        price.Unit, price.LowerPrice ?? 0, decimal.Round(amount, 2));

    private static ApiProblemException Rule(string code, string detail) =>
        new(422, code, "Shipping estimate could not be calculated", detail);
}
