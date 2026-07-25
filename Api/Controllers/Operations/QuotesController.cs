using Api.Controllers;
using Application.Common.Interfaces;
using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Route("api/operations/quotes")]
public sealed class QuotesController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IIntegrationGateway _integrations;
    private readonly IClientCommunicationService _communications;
    private readonly ICurrencyExchangeService _exchange;

    public QuotesController(IApplicationDbContext context, IIntegrationGateway integrations,
        IClientCommunicationService communications, ICurrencyExchangeService exchange)
    {
        _context = context; _integrations = integrations; _communications = communications; _exchange = exchange;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult> Create(CreateShippingQuoteRequest request, CancellationToken cancellationToken)
    {
        if (request.ActualWeightKg <= 0 || request.LengthCm <= 0 || request.WidthCm <= 0 || request.HeightCm <= 0)
            return BadRequest("Weight and all dimensions must be greater than zero.");
        var route = await _context.ServiceRoutes.FirstOrDefaultAsync(x => x.Id == request.ServiceRouteId && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Service route not found.");
        var destinationCountry = await _context.Branches.AsNoTracking().Where(x => x.Id == route.DestinationBranchId)
            .Select(x => x.CountryCode).FirstAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var rate = await _context.RateCards.Where(x => x.ServiceRouteId == route.Id && x.IsActive &&
                x.CargoCategory == request.CargoCategory && x.EffectiveFrom <= now && (!x.EffectiveTo.HasValue || x.EffectiveTo >= now))
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No active rate is configured for this route and cargo category.");
        var volumetric = Math.Round((request.LengthCm * request.WidthCm * request.HeightCm) / route.VolumetricDivisor, 2);
        var billable = Math.Max(Math.Max(request.ActualWeightKg, volumetric), rate.MinimumWeightKg);
        var baseAmount = Math.Max(Math.Round(billable * rate.RatePerKg, 2), rate.MinimumCharge);
        var fuel = Math.Round(baseAmount * rate.FuelSurchargeRate, 2);
        var customs = Math.Round(request.DeclaredValue * rate.CustomsRate, 2);
        var isStaff = User.Identity?.IsAuthenticated == true && !User.IsInRole(nameof(UserRole.Customer));
        Guid? linkedCustomerId = null;
        if (User.IsInRole(nameof(UserRole.Customer))) linkedCustomerId = CurrentCustomerId;
        else if (isStaff && request.CustomerId.HasValue && await _context.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken))
            linkedCustomerId = request.CustomerId;
        var quote = new ShippingQuote { QuoteNumber = $"BGL-QT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CustomerId = linkedCustomerId, ServiceRouteId = route.Id, Status = QuoteStatus.Sent,
            ContactName = request.ContactName.Trim(), ContactEmail = request.ContactEmail.Trim().ToLowerInvariant(),
            ContactPhone = request.ContactPhone.Trim(), CargoDescription = request.CargoDescription.Trim(),
            CargoCategory = request.CargoCategory.Trim(), ActualWeightKg = request.ActualWeightKg,
            VolumetricWeightKg = volumetric, BillableWeightKg = billable, BaseAmount = baseAmount,
            FuelSurchargeAmount = fuel, CustomsAmount = customs, TotalAmount = baseAmount + fuel + customs,
            Currency = rate.Currency, ExpiresAt = DateTime.UtcNow.AddDays(14), SalesLeadId = null };
        _context.ShippingQuotes.Add(quote);
        var localCurrency = CountryCurrency.ForCountry(destinationCountry, rate.Currency);
        CurrencyConversion? localTotal = null;
        if (!string.Equals(localCurrency, rate.Currency, StringComparison.OrdinalIgnoreCase))
            localTotal = await _exchange.ConvertAsync(quote.TotalAmount, rate.Currency, localCurrency, cancellationToken);
        await _integrations.SendNotificationAsync(IntegrationKind.Email, quote.ContactEmail, "shipping-quote-created",
            new { quote.QuoteNumber, quote.TotalAmount, quote.Currency, LocalTotal = localTotal, quote.ExpiresAt }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/quotes/{quote.Id}", new { Quote = quote, LocalTotal = localTotal });
    }

    [Authorize(Roles = nameof(UserRole.Customer))]
    [HttpGet("my")]
    public async Task<ActionResult> Mine([FromQuery] bool paged = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var ordered = _context.ShippingQuotes.AsNoTracking().Where(x => x.CustomerId == CurrentCustomerId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
        if (paged) return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
        return Ok(await ordered.Take(100).ToListAsync(cancellationToken));
    }

    [Authorize(Policy = "SalesTeam")]
    [HttpPost("{quoteId:guid}/email")]
    public async Task<ActionResult> Email(Guid quoteId, SendClientEmailRequest request, CancellationToken cancellationToken)
    {
        var quote = await _context.ShippingQuotes.FirstOrDefaultAsync(x => x.Id == quoteId, cancellationToken)
            ?? throw new KeyNotFoundException("Quotation not found.");
        if (!quote.CustomerId.HasValue) return BadRequest("Quotation must be linked to a client before it can be emailed.");
        var message = await _communications.SendAsync(quote.CustomerId.Value, CommunicationType.Quotation,
            request.Subject, request.Body, "ShippingQuote", quote.Id, request.AttachmentDocumentIds,
            new { quote.QuoteNumber, quote.CargoDescription, quote.BillableWeightKg, quote.BaseAmount,
                quote.FuelSurchargeAmount, quote.CustomsAmount, quote.TotalAmount, quote.Currency, quote.ExpiresAt }, cancellationToken);
        quote.Status = QuoteStatus.Sent; await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message.Id, message.Status, quote.QuoteNumber, Accepted = true });
    }
}
