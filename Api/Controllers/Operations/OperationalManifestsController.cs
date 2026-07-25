using System.Globalization;
using System.Net;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Api.Controllers;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

/// <summary>
/// Shipment manifests. The header identifies one shipment; invoiced customers and packages
/// are independently editable child entries.
/// </summary>
[ApiController]
[Authorize(Policy = "OperationsTeam")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/legacy/operations/manifests")]
[Obsolete("Legacy manifest workflow. Use /api/operations/manifests.")]
public sealed class OperationalManifestsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyDocumentNumberGenerator _numbers;
    private readonly IClientCommunicationService _communications;
    private readonly ICargoAllocationService _allocations;
    private readonly IClientIdentityService _identity;
    private readonly IShipmentInvoiceService _shipmentInvoices;
    private readonly IAccountingService _accounting;

    public OperationalManifestsController(IApplicationDbContext context,
        ICompanyDocumentNumberGenerator numbers, IClientCommunicationService communications,
        ICargoAllocationService allocations, IClientIdentityService identity,
        IShipmentInvoiceService shipmentInvoices, IAccountingService accounting)
    {
        _context = context;
        _numbers = numbers;
        _communications = communications;
        _allocations = allocations;
        _identity = identity;
        _shipmentInvoices = shipmentInvoices;
        _accounting = accounting;
    }

    /// <summary>Live name suggestions for the package modal. The input remains free text.</summary>
    [HttpGet("customer-name-suggestions")]
    public async Task<ActionResult<IReadOnlyList<ManifestCustomerSuggestionResponse>>> CustomerNameSuggestions(
        [FromQuery] string name, [FromQuery] int take = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return Ok(Array.Empty<ManifestCustomerSuggestionResponse>());
        var term = ClientIdentityNormalizer.Name(name);
        if (term.Length > 255) return BadRequest("Customer name search cannot exceed 255 characters.");
        var limit = Math.Clamp(take, 1, 20);
        var prefix = await _context.Customers.AsNoTracking().Where(x =>
                x.NormalizedFullName.StartsWith(term))
            .OrderBy(x => x.NormalizedFullName).ThenBy(x => x.Id).Take(limit)
            .Select(x => new ManifestCustomerSuggestionResponse(x.Id, x.FullName, x.Phone,
                x.Email, x.CountryCode, x.NormalizedFullName == term))
            .ToListAsync(cancellationToken);
        if (prefix.Count == limit) return Ok(prefix);
        var prefixIds = prefix.Select(x => x.CustomerId).ToList();
        var contains = await _context.Customers.AsNoTracking().Where(x =>
                !prefixIds.Contains(x.Id) && x.NormalizedFullName.Contains(term))
            .OrderBy(x => x.NormalizedFullName).ThenBy(x => x.Id).Take(limit - prefix.Count)
            .Select(x => new ManifestCustomerSuggestionResponse(x.Id, x.FullName, x.Phone,
                x.Email, x.CountryCode, x.NormalizedFullName == term))
            .ToListAsync(cancellationToken);
        prefix.AddRange(contains);
        return Ok(prefix);
    }

    [HttpPost]
    public async Task<ActionResult<OperationalManifestResponse>> Create(
        CreateOperationalManifestRequest request, CancellationToken cancellationToken)
    {
        var error = ValidateHeader(request);
        if (error is not null) return BadRequest(error);
        var shipmentId = NormalizeIdentifier(request.ShipmentId);
        var bookingId = NormalizeIdentifier(request.ShipmentBookingId);
        if (await _context.OperationalManifests.AsNoTracking().AnyAsync(x =>
                x.ShipmentId == shipmentId, cancellationToken))
            return Conflict("Shipment ID is already in use.");
        if (await _context.OperationalManifests.AsNoTracking().AnyAsync(x =>
                x.ShipmentBookingId == bookingId, cancellationToken))
            return Conflict("Shipment booking ID is already in use.");
        var route = await ResolveRouteAsync(request.ServiceRouteId, cancellationToken);
        if (route.Method != request.LogisticsMethod)
            return BadRequest($"The selected route is {route.Method}; update the logistics method to match it.");
        var manifest = new OperationalManifest
        {
            ManifestNumber = await _numbers.NextOperationalManifestAsync(cancellationToken),
            ShipmentId = shipmentId,
            ShipmentBookingId = bookingId,
            ManifestDate = UtcDate(request.ManifestDate),
            LogisticsMethod = request.LogisticsMethod,
            CreatedByUserId = CurrentUserId
        };
        ApplyRoute(manifest, route);
        _context.OperationalManifests.Add(manifest);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = manifest.Id }, EmptyResponse(manifest));
    }

    /// <summary>Edits only the shipment ID/name and shipment booking ID.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OperationalManifestResponse>> Update(Guid id,
        UpdateOperationalManifestRequest request, CancellationToken cancellationToken)
    {
        var error = ValidateIdentity(request);
        if (error is not null) return BadRequest(error);
        var manifest = await _context.OperationalManifests.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment manifest not found.");
        var shipmentId = NormalizeIdentifier(request.ShipmentId);
        var bookingId = NormalizeIdentifier(request.ShipmentBookingId);
        if (await _context.OperationalManifests.AsNoTracking().AnyAsync(x => x.Id != id &&
                x.ShipmentId == shipmentId, cancellationToken)) return Conflict("Shipment ID is already in use.");
        if (await _context.OperationalManifests.AsNoTracking().AnyAsync(x => x.Id != id &&
                x.ShipmentBookingId == bookingId, cancellationToken))
            return Conflict("Shipment booking ID is already in use.");
        manifest.ShipmentId = shipmentId;
        manifest.ShipmentBookingId = bookingId;
        manifest.LastEditedByUserId = CurrentUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok((await MapHeadersAsync(new[] { manifest }, cancellationToken)).Single());
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search,
        [FromQuery] ManifestLogisticsMethod? logisticsMethod, [FromQuery] PaymentMethod? paymentType,
        [FromQuery] ShipmentChargeBasis? chargeBasis,
        [FromQuery] Guid? customerId, [FromQuery] string? invoiceNumber,
        [FromQuery] Guid? serviceRouteId, [FromQuery] string? originCountryCode,
        [FromQuery] string? destinationCountryCode,
        [FromQuery] DateTime? date, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.OperationalManifests.AsNoTracking().AsQueryable();
        if (logisticsMethod.HasValue) query = query.Where(x => x.LogisticsMethod == logisticsMethod);
        if (paymentType.HasValue)
            query = query.Where(x => _context.OperationalManifestEntries.Any(y =>
                y.ManifestId == x.Id && y.PaymentType == paymentType));
        if (chargeBasis.HasValue)
            query = query.Where(x => _context.OperationalManifestEntries.Any(y =>
                y.ManifestId == x.Id && y.ChargeBasis == chargeBasis));
        if (customerId.HasValue)
            query = query.Where(x => _context.OperationalManifestEntries.Any(y =>
                y.ManifestId == x.Id && y.CustomerId == customerId));
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var invoice = NormalizeIdentifier(invoiceNumber);
            query = query.Where(x => _context.OperationalManifestEntries.Any(y =>
                y.ManifestId == x.Id && y.InvoiceNumberSnapshot == invoice));
        }
        if (serviceRouteId.HasValue) query = query.Where(x => x.ServiceRouteId == serviceRouteId);
        if (!string.IsNullOrWhiteSpace(originCountryCode))
        {
            var origin = originCountryCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.OriginCountryCode == origin);
        }
        if (!string.IsNullOrWhiteSpace(destinationCountryCode))
        {
            var destination = destinationCountryCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.DestinationCountryCode == destination);
        }
        if (date.HasValue)
        {
            var selected = UtcDate(date.Value);
            query = query.Where(x => x.ManifestDate >= selected && x.ManifestDate < selected.AddDays(1));
        }
        else
        {
            if (from.HasValue) query = query.Where(x => x.ManifestDate >= UtcDate(from.Value));
            if (to.HasValue) query = query.Where(x => x.ManifestDate < UtcDate(to.Value).AddDays(1));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.ManifestNumber.ToLower().Contains(term) ||
                x.ShipmentId.ToLower().Contains(term) || x.ShipmentBookingId.ToLower().Contains(term) ||
                x.RouteCode.ToLower().Contains(term) || x.RouteName.ToLower().Contains(term) ||
                _context.OperationalManifestEntries.Any(y => y.ManifestId == x.Id &&
                    (y.CustomerNameSnapshot.ToLower().Contains(term) ||
                     y.ReceiverName.ToLower().Contains(term) ||
                     (y.ReceiverPhone != null && y.ReceiverPhone.ToLower().Contains(term)) ||
                     (y.ReceiverEmail != null && y.ReceiverEmail.ToLower().Contains(term)) ||
                     y.SenderName.ToLower().Contains(term) ||
                     (y.InvoiceNumberSnapshot != null && y.InvoiceNumberSnapshot.ToLower().Contains(term)) ||
                     (y.TrackingNumberSnapshot != null && y.TrackingNumberSnapshot.ToLower().Contains(term)))));
        }
        var pageResult = await query.OrderByDescending(x => x.ManifestDate)
            .ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
        var items = await MapHeadersAsync(pageResult.Items, cancellationToken);
        return Ok(new PagedResponse<OperationalManifestResponse>(items, pageResult.Page,
            pageResult.PageSize, pageResult.HasPrevious, pageResult.HasNext,
            pageResult.Total, pageResult.TotalPages));
    }

    [HttpGet("date-groups")]
    public async Task<ActionResult> DateGroups([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var end = UtcDate(to ?? DateTime.UtcNow);
        var start = UtcDate(from ?? end.AddDays(-30));
        if (start > end) return BadRequest("The start date must not be after the end date.");
        if (end - start > TimeSpan.FromDays(366))
            return BadRequest("Manifest date grouping supports at most 366 days.");
        var manifests = await _context.OperationalManifests.AsNoTracking().Where(x =>
                x.ManifestDate >= start && x.ManifestDate < end.AddDays(1))
            .ToListAsync(cancellationToken);
        var ids = manifests.Select(x => x.Id).ToList();
        var entries = await _context.OperationalManifestEntries.AsNoTracking().Where(x =>
                ids.Contains(x.ManifestId)).Select(x => new EntryGroupRow(x.ManifestId, x.Id,
                x.CustomerId, x.CargoItemId, x.ActualWeightKg, x.CostWeightKg, x.Amount, x.Currency))
            .ToListAsync(cancellationToken);
        var byManifest = entries.ToLookup(x => x.ManifestId);
        var rows = manifests.Select(x => new ManifestGroupRow(x.Id, x.ManifestDate, x.ServiceRouteId,
            x.RouteCode, x.RouteName, x.OriginCountryCode, x.DestinationCountryCode,
            x.LogisticsMethod, byManifest[x.Id].ToList())).ToList();
        var days = rows.GroupBy(x => x.ManifestDate.Date).OrderByDescending(x => x.Key).Select(x => new
        {
            Date = x.Key,
            Air = Summarize(x, ManifestLogisticsMethod.Air),
            Ship = Summarize(x, ManifestLogisticsMethod.Ship),
            Directions = BuildDirections(x)
        }).ToList();
        return Ok(new
        {
            From = start,
            To = end,
            Days = days,
            AllShipments = new
            {
                Air = Summarize(rows, ManifestLogisticsMethod.Air),
                Ship = Summarize(rows, ManifestLogisticsMethod.Ship)
            },
            Directions = BuildDirections(rows)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperationalManifestResponse>> Get(Guid id,
        CancellationToken cancellationToken)
    {
        var manifest = await _context.OperationalManifests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment manifest not found.");
        return Ok((await MapHeadersAsync(new[] { manifest }, cancellationToken)).Single());
    }

    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult> Entries(Guid id, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        if (!await _context.OperationalManifests.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken))
            return NotFound();
        var query = EntryQuery(manifestId: id, search: search);
        return Ok(await query.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpPost("{id:guid}/entries")]
    public async Task<ActionResult<OperationalManifestEntryResponse>> AddEntry(Guid id,
        CreateOperationalManifestEntryRequest request, CancellationToken cancellationToken)
    {
        var manifest = await _context.OperationalManifests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment manifest not found.");
        var validation = ValidateCreateEntry(request, manifest.LogisticsMethod);
        if (validation is not null) return BadRequest(validation);
        var pricing = CalculatePricing(request.Pricing);
        var customerResolution = await ResolveCustomerAsync(request.Customer, cancellationToken);
        if (customerResolution.DuplicateReview is not null)
            return Conflict(customerResolution.DuplicateReview);
        var customer = customerResolution.Customer!;
        Customer? referrer = null;
        if (request.ReferrerCustomerId.HasValue)
        {
            referrer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == request.ReferrerCustomerId.Value, cancellationToken);
            if (referrer is null) return BadRequest("Select a referrer from the existing customer suggestions.");
            if (referrer.Id == customer.Id) return BadRequest("A customer cannot refer themselves.");
        }
        if (request.CargoItemId.HasValue && await _context.OperationalManifestEntries.AsNoTracking()
                .AnyAsync(x => x.ManifestId == id && x.CargoItemId == request.CargoItemId, cancellationToken))
            return Conflict("This package is already in the shipment manifest; edit its existing row.");
        var issuer = await ResolveManifestIssuerAsync(manifest, cancellationToken);
        var createdInvoice = await _shipmentInvoices.CreateAsync(new CreateShipmentInvoiceCommand(
            customer.Id, customer.FullName, issuer.Code, CountryCurrency.ForCountry(issuer.CountryCode),
            pricing.TotalAmount, request.Invoice.PaymentMethod, pricing.Lines.Select(x =>
                new CreateShipmentInvoiceLineCommand(x.InvoiceDescription, x.InvoiceQuantity,
                    x.UnitPrice)).ToList(),
            manifest.ShipmentId), cancellationToken);
        var resolved = await ResolveNewEntryAsync(manifest, customer, createdInvoice.Invoice,
            request, pricing, cancellationToken);
        var entry = BuildEntry(id, resolved);
        entry.ReferrerCustomerId = referrer?.Id;
        entry.ReferrerNameSnapshot = referrer?.FullName;
        entry.ReferralPointsAwarded = referrer is null ? 0 : LoyaltyPointsPolicy.PaidReferralShipmentPoints;
        if (customerResolution.IsNew)
        {
            _context.Customers.Add(customer);
            _context.NotificationPreferences.Add(new NotificationPreference { CustomerId = customer.Id });
            _context.Contacts.Add(new Contact
            {
                Type = ContactType.Client,
                Status = ContactStatus.Active,
                FullName = customer.FullName,
                Email = customer.Email,
                Phone = customer.Phone,
                CountryCode = customer.CountryCode,
                CustomerId = customer.Id,
                PreferredBranchId = issuer.Id,
                IsPrimary = true,
                Tags = "Shipment customer",
                Notes = $"Created automatically from shipment {manifest.ShipmentId}."
            });
        }
        _context.Invoices.Add(createdInvoice.Invoice);
        _context.InvoiceLines.AddRange(createdInvoice.Lines);
        var paymentBundle = await CreateInitialPaymentAsync(createdInvoice.Invoice,
            request.Invoice.PaymentMethod, cancellationToken);
        _context.Payments.Add(paymentBundle.Payment);
        _context.FinancialTransactions.Add(paymentBundle.Transaction);
        _context.TransactionDocuments.Add(paymentBundle.Receipt);
        if (referrer is not null)
            await AwardReferralPointsAsync(referrer, customer, createdInvoice.Invoice,
                entry.ReferralPointsAwarded, cancellationToken);
        _context.OperationalManifestEntries.Add(entry);
        _context.OperationalManifestChargeLines.AddRange(pricing.Lines.Select(x =>
            BuildChargeLine(entry.Id, x)));
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/manifests/{id}/entries/{entry.Id}",
            await MapEntryAsync(entry.Id, cancellationToken));
    }

    [HttpPut("{id:guid}/entries/{entryId:guid}")]
    public async Task<ActionResult<OperationalManifestEntryResponse>> UpdateEntry(Guid id, Guid entryId,
        UpdateOperationalManifestEntryRequest request, CancellationToken cancellationToken)
    {
        var manifest = await _context.OperationalManifests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment manifest not found.");
        var current = await _context.OperationalManifestEntries.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == entryId && x.ManifestId == id, cancellationToken)
            ?? throw new KeyNotFoundException("Manifest customer/package entry not found.");
        var validation = ValidateUpdateEntry(request);
        if (validation is not null) return BadRequest(validation);
        if (request.CargoItemId.HasValue && await _context.OperationalManifestEntries.AsNoTracking()
                .AnyAsync(x => x.ManifestId == id && x.CargoItemId == request.CargoItemId && x.Id != entryId,
                    cancellationToken))
            return Conflict("This package already has another row in the shipment manifest.");
        var resolved = await ResolveUpdatedEntryAsync(manifest, current, request, cancellationToken);
        var entry = await _context.OperationalManifestEntries.FirstAsync(x => x.Id == entryId,
            cancellationToken);
        ApplyOperationalEntry(entry, resolved);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(await MapEntryAsync(entry.Id, cancellationToken));
    }

    /// <summary>Returns the generated invoice in a separate, read-only projection.</summary>
    [HttpGet("{id:guid}/entries/{entryId:guid}/invoice")]
    public async Task<ActionResult<OperationalManifestInvoiceResponse>> EntryInvoice(Guid id, Guid entryId,
        CancellationToken cancellationToken)
    {
        var invoice = await (from entry in _context.OperationalManifestEntries.AsNoTracking()
                             join value in _context.Invoices.AsNoTracking() on entry.InvoiceId equals value.Id
                             where entry.ManifestId == id && entry.Id == entryId
                             select value).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Generated shipment invoice not found.");
        var lines = await _context.InvoiceLines.AsNoTracking().Where(x => x.InvoiceId == invoice.Id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new OperationalManifestInvoiceLineResponse(x.Id, x.Description, x.Quantity,
                x.UnitPrice, x.TaxRate, x.LineTotal)).ToListAsync(cancellationToken);
        var payment = await _context.Payments.AsNoTracking().Where(x => x.InvoiceId == invoice.Id)
            .OrderBy(x => x.PaidAt).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        var receipt = payment is null ? null : await _context.TransactionDocuments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PaymentId == payment.Id && x.Type == FinancialDocumentType.Receipt,
                cancellationToken);
        return Ok(new OperationalManifestInvoiceResponse(invoice.Id, invoice.InvoiceNumber,
            invoice.CustomerId, invoice.CustomerName, invoice.IssuerBranchCode, invoice.IssueDate,
            invoice.DueDate, invoice.Status, invoice.PaymentStatus, invoice.PreferredPaymentMethod,
            invoice.Currency, invoice.TotalAmount, invoice.AmountPaid, invoice.BalanceDue,
            payment?.Id, payment?.Reference, payment?.PaidAt, receipt?.Id, receipt?.DocumentNumber,
            true, lines));
    }

    /// <summary>
    /// Returns the immutable evidence behind the shipment price. The dashboard uses this to
    /// explain Air volumetric calculations, Sea box/item totals, or a negotiated exception.
    /// </summary>
    [HttpGet("{id:guid}/entries/{entryId:guid}/pricing")]
    public async Task<ActionResult<OperationalManifestPricingResponse>> EntryPricing(Guid id,
        Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await _context.OperationalManifestEntries.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ManifestId == id && x.Id == entryId, cancellationToken)
            ?? throw new KeyNotFoundException("Manifest customer/package entry not found.");
        var lines = await _context.OperationalManifestChargeLines.AsNoTracking()
            .Where(x => x.ManifestEntryId == entryId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new OperationalManifestChargeLineResponse(x.Id, x.Type, x.Description,
                x.BoxSize, x.Quantity, x.UnitPrice, x.LengthCm, x.WidthCm, x.HeightCm,
                x.ActualWeightKg, x.VolumetricDivisor, x.VolumetricWeightKg,
                x.BillableWeightKg, x.LineAmount, x.SortOrder))
            .ToListAsync(cancellationToken);
        return Ok(new OperationalManifestPricingResponse(entry.Id, entry.ChargeBasis,
            entry.NegotiationReference, entry.Currency, entry.Amount, entry.ActualWeightKg,
            entry.CostWeightKg, lines));
    }

    [HttpDelete("{id:guid}/entries/{entryId:guid}")]
    public async Task<IActionResult> RemoveEntry(Guid id, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await _context.OperationalManifestEntries.FirstOrDefaultAsync(x =>
            x.Id == entryId && x.ManifestId == id, cancellationToken)
            ?? throw new KeyNotFoundException("Manifest customer/package entry not found.");
        _context.OperationalManifestEntries.Remove(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/entries/{entryId:guid}/release-check")]
    public async Task<ActionResult> VerifyEntryRelease(Guid id, Guid entryId,
        VerifyManifestEntryReleaseRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
            return BadRequest("The customer must present an invoice number.");
        var row = await (from manifest in _context.OperationalManifests.AsNoTracking()
                         join entry in _context.OperationalManifestEntries.AsNoTracking()
                             on manifest.Id equals entry.ManifestId
                         where manifest.Id == id && entry.Id == entryId
                         select new { Manifest = manifest, Entry = entry }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Manifest customer/package entry not found.");
        var presented = NormalizeIdentifier(request.InvoiceNumber);
        var verified = !string.IsNullOrWhiteSpace(row.Entry.InvoiceNumberSnapshot) &&
            string.Equals(presented, row.Entry.InvoiceNumberSnapshot, StringComparison.Ordinal);
        return Ok(new
        {
            Verified = verified,
            Message = verified
                ? "Invoice confirmed. Continue the physical identity and package checks before handover."
                : "Invoice does not match this manifest entry. Do not hand it over.",
            row.Manifest.ManifestNumber,
            row.Manifest.ShipmentId,
            row.Entry.TrackingNumberSnapshot,
            CustomerName = verified ? row.Entry.CustomerNameSnapshot : null,
            SenderName = verified ? row.Entry.SenderName : null,
            HandoverMethod = verified ? row.Entry.HandoverMethod : (ManifestHandoverMethod?)null,
            HandoverLabel = verified ? HandoverLabel(row.Entry.HandoverMethod) : null
        });
    }

    [HttpPost("{id:guid}/entries/{entryId:guid}/email")]
    public async Task<ActionResult> EmailEntry(Guid id, Guid entryId, SendClientEmailRequest request,
        CancellationToken cancellationToken)
    {
        var manifest = await _context.OperationalManifests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment manifest not found.");
        var entry = await _context.OperationalManifestEntries.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == entryId && x.ManifestId == id, cancellationToken)
            ?? throw new KeyNotFoundException("Manifest customer/package entry not found.");
        var message = await _communications.SendAsync(entry.CustomerId, CommunicationType.Manifest,
            request.Subject, request.Body, "OperationalManifestEntry", entry.Id,
            request.AttachmentDocumentIds, new
            {
                manifest.ManifestNumber, manifest.ShipmentId, manifest.ShipmentBookingId,
                manifest.ManifestDate, manifest.RouteCode, manifest.RouteName, manifest.LogisticsMethod,
                entry.InvoiceNumberSnapshot, entry.CustomerNameSnapshot, entry.CustomerPhoneSnapshot,
                entry.CustomerEmailSnapshot, entry.ReceiverName, entry.ReceiverPhone,
                entry.ReceiverEmail, entry.ChargeBasis, entry.NegotiationReference,
                entry.ReferrerCustomerId, entry.ReferrerNameSnapshot, entry.ReferralPointsAwarded,
                entry.SenderName, entry.PackageDescription, entry.Quantity, entry.ActualWeightKg,
                entry.CostWeightKg, entry.Amount, entry.Currency, entry.PaymentType,
                entry.HandoverMethod, entry.HandoverAddress, entry.HandoverCity
            }, cancellationToken);
        return Ok(new { message.Id, message.Status, manifest.ManifestNumber,
            manifest.ShipmentId, EntryId = entry.Id, Accepted = true });
    }

    [HttpGet("{id:guid}/print")]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        var manifest = await _context.OperationalManifests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment manifest not found.");
        var entries = await EntryQuery(manifestId: id).ToListAsync(cancellationToken);
        var company = await _context.CompanyProfiles.AsNoTracking().OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
        var html = new StringBuilder($"<!doctype html><html><head><meta charset='utf-8'><title>{H(manifest.ShipmentId)}</title>");
        html.Append("<style>body{font-family:Arial,sans-serif;margin:30px;color:#172033}.brand{font-size:23px;font-weight:700;border-bottom:3px solid #183b62;padding-bottom:10px}" +
            ".grid{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin:18px 0}.box{padding:10px;border:1px solid #d4dbe4;border-radius:6px}" +
            ".method,.label{display:inline-block;padding:5px 9px;border-radius:14px;background:#e8f0f8;font-weight:700}.label{font-size:11px}" +
            "table{width:100%;border-collapse:collapse;margin-top:18px;font-size:12px}th,td{border:1px solid #c8d0da;padding:7px;text-align:left;vertical-align:top}th{background:#edf2f7}" +
            ".footer{margin-top:30px;border-top:1px solid #bbb;padding-top:10px;font-size:12px;font-weight:700}@media print{button{display:none}body{margin:10mm}}</style></head><body>");
        html.Append($"<button onclick='window.print()'>Print</button><div class='brand'>{H(company?.TradingName ?? "BiGSAM Global Logistics")}</div>" +
            $"<h1>Shipment Manifest</h1><span class='method'>{H(manifest.RouteName)} · {H(manifest.LogisticsMethod.ToString())}</span><div class='grid'>" +
            $"<div class='box'><b>Shipment ID:</b> {H(manifest.ShipmentId)}</div><div class='box'><b>Shipment booking ID:</b> {H(manifest.ShipmentBookingId)}</div>" +
            $"<div class='box'><b>Internal manifest:</b> {H(manifest.ManifestNumber)}</div><div class='box'><b>Date:</b> {manifest.ManifestDate:yyyy-MM-dd}</div>" +
            $"<div class='box'><b>Direction:</b> {H(manifest.OriginCountryCode)} → {H(manifest.DestinationCountryCode)}</div><div class='box'><b>Route:</b> {H(manifest.RouteCode)}</div></div>");
        html.Append("<table><thead><tr><th>Customer / owner</th><th>Receiver</th><th>Sender</th><th>Invoice</th><th>Tracking</th><th>Handover</th><th>Container</th><th>Qty</th><th>Actual kg</th><th>Pickup / delivery location</th></tr></thead><tbody>");
        foreach (var entry in entries)
            html.Append($"<tr><td>{H(entry.CustomerName)}</td><td>{H(entry.ReceiverName)}</td><td>{H(entry.SenderName)}</td><td>{H(entry.InvoiceNumber ?? "Not invoiced")}</td>" +
                $"<td>{H(entry.TrackingNumber ?? "Not linked")}</td><td><span class='label'>{H(entry.HandoverLabel)}</span></td>" +
                $"<td>{H(entry.ContainerName ?? entry.ContainerNumber ?? "Not assigned")}</td><td>{entry.Quantity:N3}</td>" +
                $"<td>{entry.ActualWeightKg:N3}</td><td>{H(JoinAddress(entry.HandoverAddress, entry.HandoverCity))}</td></tr>");
        html.Append("</tbody></table><div class='footer'>Release control: hand packages only to the recorded owner after the invoice number has been presented and confirmed. Financial values, payment details, package contents and cost weight are intentionally excluded from this operational print.</div></body></html>");
        return Content(html.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("{id:guid}/report")]
    [EnableRateLimiting("Reports")]
    [RequestTimeout("Reports")]
    public async Task<IActionResult> DownloadReport(Guid id, CancellationToken cancellationToken)
    {
        var manifest = await _context.OperationalManifests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Shipment manifest not found.");
        var entries = await EntryQuery(manifestId: id).ToListAsync(cancellationToken);
        var csv = new StringBuilder();
        csv.AppendLine(Csv("Manifest Number", "Shipment ID", "Shipment Booking ID", "Manifest Date",
            "Route", "Direction", "Logistics Method", "Customer / Owner", "Customer Phone",
            "Customer Email", "Receiver Name", "Receiver Phone", "Receiver Email",
            "Invoice Number", "Invoiced", "Tracking Number", "Sender",
            "Referrer", "Referral Points", "Package Description", "Quantity", "Actual Weight Kg", "Cost Weight Kg", "Amount",
            "Currency", "Payment Type", "Charge Basis", "Negotiation Reference",
            "Pricing Breakdown", "Handover Method", "Handover Address", "Handover City",
            "Batch", "Movement", "Container Number", "Container Name", "Notes", "Entry Added At",
            "Entry Last Edited At", "Manifest Created At", "Manifest Last Edited At"));
        var entryIds = entries.Select(x => x.Id).ToList();
        var pricingLines = await _context.OperationalManifestChargeLines.AsNoTracking()
            .Where(x => entryIds.Contains(x.ManifestEntryId))
            .OrderBy(x => x.ManifestEntryId).ThenBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new { x.ManifestEntryId, x.Type, x.Description, x.BoxSize, x.Quantity,
                x.UnitPrice, x.LengthCm, x.WidthCm, x.HeightCm, x.ActualWeightKg,
                x.VolumetricWeightKg, x.BillableWeightKg, x.LineAmount })
            .ToListAsync(cancellationToken);
        var pricingByEntry = pricingLines.ToLookup(x => x.ManifestEntryId);
        foreach (var entry in entries)
        {
            var breakdown = string.Join(" | ", pricingByEntry[entry.Id].Select(x =>
                $"{x.Type}: {x.Description}; size={x.BoxSize ?? "-"}; qty={x.Quantity}; " +
                $"unit={x.UnitPrice}; dimensions={x.LengthCm}x{x.WidthCm}x{x.HeightCm}cm; " +
                $"actual={x.ActualWeightKg}; volumetric={x.VolumetricWeightKg}; " +
                $"billable={x.BillableWeightKg}; total={x.LineAmount}"));
            csv.AppendLine(Csv(manifest.ManifestNumber, manifest.ShipmentId, manifest.ShipmentBookingId,
                manifest.ManifestDate, $"{manifest.RouteCode} - {manifest.RouteName}",
                $"{manifest.OriginCountryCode}->{manifest.DestinationCountryCode}", manifest.LogisticsMethod,
                entry.CustomerName, entry.CustomerPhone, entry.CustomerEmail, entry.ReceiverName,
                entry.ReceiverPhone, entry.ReceiverEmail, entry.InvoiceNumber,
                entry.IsInvoiced, entry.TrackingNumber, entry.SenderName, entry.ReferrerName,
                entry.ReferralPointsAwarded, entry.PackageDescription,
                entry.Quantity, entry.ActualWeightKg, entry.CostWeightKg, entry.Amount, entry.Currency,
                entry.PaymentType, entry.ChargeBasis, entry.NegotiationReference, breakdown,
                entry.HandoverMethod, entry.HandoverAddress, entry.HandoverCity,
                entry.BatchCode, entry.MovementCode, entry.ContainerNumber, entry.ContainerName, entry.Notes,
                entry.AddedAt, entry.LastModifiedAt, manifest.CreatedAt, manifest.LastModifiedAt));
        }
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8",
            $"{manifest.ShipmentId}-complete-manifest-report.csv");
    }

    private IQueryable<OperationalManifestEntryResponse> EntryQuery(Guid? entryId = null,
        Guid? manifestId = null, string? search = null)
    {
        var source = _context.OperationalManifestEntries.AsNoTracking().AsQueryable();
        if (entryId.HasValue) source = source.Where(x => x.Id == entryId.Value);
        if (manifestId.HasValue) source = source.Where(x => x.ManifestId == manifestId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            source = source.Where(x => x.CustomerNameSnapshot.ToLower().Contains(term) ||
                x.ReceiverName.ToLower().Contains(term) ||
                (x.ReceiverPhone != null && x.ReceiverPhone.ToLower().Contains(term)) ||
                (x.ReceiverEmail != null && x.ReceiverEmail.ToLower().Contains(term)) ||
                x.SenderName.ToLower().Contains(term) || x.PackageDescription.ToLower().Contains(term) ||
                (x.InvoiceNumberSnapshot != null && x.InvoiceNumberSnapshot.ToLower().Contains(term)) ||
                (x.TrackingNumberSnapshot != null && x.TrackingNumberSnapshot.ToLower().Contains(term)) ||
                (x.ContainerNumberSnapshot != null && x.ContainerNumberSnapshot.ToLower().Contains(term)));
        }
        var ordered = source.OrderBy(x => x.CustomerNameSnapshot).ThenBy(x => x.AddedAt)
            .ThenBy(x => x.Id);
        return from entry in ordered
        join cargo in _context.CargoItems.AsNoTracking() on entry.CargoItemId equals cargo.Id into cargos
        from cargo in cargos.DefaultIfEmpty()
        join batch in _context.ShippingBatches.AsNoTracking() on entry.ShippingBatchId equals batch.Id into batches
        from batch in batches.DefaultIfEmpty()
        join movement in _context.ShipmentMovements.AsNoTracking() on entry.ShipmentMovementId equals movement.Id into movements
        from movement in movements.DefaultIfEmpty()
        join container in _context.ShipmentContainers.AsNoTracking() on entry.ShipmentContainerId equals container.Id into containers
        from container in containers.DefaultIfEmpty()
        join referrer in _context.Customers.AsNoTracking() on entry.ReferrerCustomerId equals referrer.Id into referrers
        from referrer in referrers.DefaultIfEmpty()
        select new OperationalManifestEntryResponse(entry.Id, entry.ManifestId, entry.CustomerId,
            entry.CustomerNameSnapshot, entry.CustomerPhoneSnapshot, entry.CustomerEmailSnapshot,
            entry.ReceiverName, entry.ReceiverPhone, entry.ReceiverEmail,
            entry.InvoiceId, entry.InvoiceNumberSnapshot, true, entry.ReferrerCustomerId,
            referrer == null ? entry.ReferrerNameSnapshot : referrer.FullName, entry.ReferralPointsAwarded,
            entry.CargoItemId, cargo == null ? entry.TrackingNumberSnapshot : cargo.TrackingNumber,
            entry.SenderName, entry.PackageDescription, entry.Quantity, entry.ActualWeightKg,
            entry.CostWeightKg, entry.Amount, entry.Currency, entry.PaymentType,
            entry.ChargeBasis, entry.NegotiationReference, entry.HandoverMethod,
            entry.HandoverMethod == ManifestHandoverMethod.HubCollection ? "Pickup" : "Delivery",
            entry.HandoverAddress, entry.HandoverCity, entry.ShippingBatchId,
            batch == null ? null : batch.BatchCode, entry.ShipmentMovementId,
            movement == null ? entry.MovementCodeSnapshot : movement.MovementCode,
            entry.ShipmentContainerId,
            container == null ? entry.ContainerNumberSnapshot : container.ContainerNumber,
            container == null ? null : container.DisplayName ?? container.ContainerNumber,
            entry.Notes, entry.AddedAt, entry.LastModifiedAt);
    }

    private async Task<OperationalManifestEntryResponse> MapEntryAsync(Guid entryId,
        CancellationToken cancellationToken) => await EntryQuery(entryId).SingleAsync(cancellationToken);

    private async Task<IReadOnlyList<OperationalManifestResponse>> MapHeadersAsync(
        IReadOnlyList<OperationalManifest> manifests, CancellationToken cancellationToken)
    {
        if (manifests.Count == 0) return Array.Empty<OperationalManifestResponse>();
        var ids = manifests.Select(x => x.Id).ToList();
        var stats = await _context.OperationalManifestEntries.AsNoTracking().Where(x => ids.Contains(x.ManifestId))
            .GroupBy(x => x.ManifestId).Select(x => new
            {
                ManifestId = x.Key,
                EntryCount = x.Count(),
                CustomerCount = x.Select(y => y.CustomerId).Distinct().Count(),
                PackageCount = x.Count(y => y.CargoItemId != null),
                ActualWeightKg = x.Sum(y => y.ActualWeightKg),
                CostWeightKg = x.Sum(y => y.CostWeightKg)
            }).ToDictionaryAsync(x => x.ManifestId, cancellationToken);
        var amountRows = await _context.OperationalManifestEntries.AsNoTracking().Where(x =>
                ids.Contains(x.ManifestId)).GroupBy(x => new { x.ManifestId, x.Currency })
            .Select(x => new { x.Key.ManifestId, x.Key.Currency, Amount = x.Sum(y => y.Amount) })
            .ToListAsync(cancellationToken);
        var amounts = amountRows.GroupBy(x => x.ManifestId).ToDictionary(x => x.Key,
            x => (IReadOnlyList<ManifestCurrencyAmountResponse>)x.OrderBy(y => y.Currency)
                .Select(y => new ManifestCurrencyAmountResponse(y.Currency, y.Amount)).ToList());
        return manifests.Select(x =>
        {
            stats.TryGetValue(x.Id, out var stat);
            return new OperationalManifestResponse(x.Id, x.ManifestNumber, x.ShipmentId,
                x.ShipmentBookingId, x.ServiceRouteId, x.RouteCode, x.RouteName,
                x.OriginCountryCode, x.DestinationCountryCode, x.ManifestDate, x.LogisticsMethod,
                stat?.EntryCount ?? 0, stat?.CustomerCount ?? 0, stat?.PackageCount ?? 0,
                stat?.ActualWeightKg ?? 0, stat?.CostWeightKg ?? 0,
                amounts.GetValueOrDefault(x.Id) ?? Array.Empty<ManifestCurrencyAmountResponse>(),
                x.CreatedAt, x.LastModifiedAt);
        }).ToList();
    }

    private static OperationalManifestResponse EmptyResponse(OperationalManifest x) => new(x.Id,
        x.ManifestNumber, x.ShipmentId, x.ShipmentBookingId, x.ServiceRouteId, x.RouteCode,
        x.RouteName, x.OriginCountryCode, x.DestinationCountryCode, x.ManifestDate,
        x.LogisticsMethod, 0, 0, 0, 0, 0, Array.Empty<ManifestCurrencyAmountResponse>(),
        x.CreatedAt, x.LastModifiedAt);

    private async Task<CustomerResolution> ResolveCustomerAsync(ManifestEntryCustomerInput input,
        CancellationToken cancellationToken)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Customer name is required.");
        if (input.Name.Trim().Length > 255) throw new ArgumentException("Customer name is too long.");
        if (input.ExistingCustomerId.HasValue)
        {
            var existing = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == input.ExistingCustomerId, cancellationToken)
                ?? throw new KeyNotFoundException("Selected customer no longer exists.");
            if (existing.NormalizedFullName != ClientIdentityNormalizer.Name(input.Name))
                throw new InvalidOperationException("The selected customer does not match the entered name.");
            return new CustomerResolution(existing, false, null);
        }

        var email = input.Email?.Trim() ?? string.Empty;
        var phone = input.Phone?.Trim() ?? string.Empty;
        var country = input.CountryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("A new customer requires name, phone, email, and country.");
        if (!new EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("Enter a valid customer email address.");
        if (email.Length > 255 || phone.Length > 40 || country.Length > 5)
            throw new ArgumentException("One or more new customer details exceed the supported length.");
        var identity = new ClientIdentityInput(input.Name, email, phone, null);
        var selectedId = await _identity.GetSelectedExistingClientAsync(identity,
            input.DuplicateReviewId, cancellationToken);
        if (selectedId.HasValue)
        {
            var selected = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == selectedId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Selected customer no longer exists.");
            return new CustomerResolution(selected, false, null);
        }
        var duplicate = await _identity.ValidateNewClientAsync(identity, input.DuplicateReviewId,
            null, cancellationToken);
        if (duplicate is not null) return new CustomerResolution(null, false, duplicate);
        var customer = new Customer(Guid.NewGuid(), input.Name.Trim(), email, 0, country)
        {
            Phone = phone,
            WalletCurrency = CountryCurrency.ForCountry(country),
            AccountType = ClientAccountType.Individual,
            IsBusiness = false
        };
        return new CustomerResolution(customer, true, null);
    }

    private async Task<ResolvedEntry> ResolveNewEntryAsync(OperationalManifest manifest,
        Customer customer, Invoice invoice, CreateOperationalManifestEntryRequest request,
        PricingCalculation pricing, CancellationToken cancellationToken)
    {
        CargoItem? cargo = null;
        if (request.CargoItemId.HasValue)
        {
            cargo = await _context.CargoItems.FirstOrDefaultAsync(x =>
                x.Id == request.CargoItemId, cancellationToken)
                ?? throw new KeyNotFoundException("Cargo/package not found.");
            if (cargo.CustomerId != customer.Id)
                throw new InvalidOperationException("The package belongs to a different customer.");
            if (cargo.InvoiceId.HasValue)
                throw new InvalidOperationException("The package belongs to a different invoice.");
            cargo.InvoiceId = invoice.Id;
        }
        var batchId = request.ShippingBatchId ?? cargo?.BatchId;
        if (batchId.HasValue && !await _context.ShippingBatches.AsNoTracking().AnyAsync(x =>
                x.Id == batchId, cancellationToken)) throw new KeyNotFoundException("Shipping batch not found.");
        return new ResolvedEntry(customer, invoice, cargo, batchId, null, null, invoice.Currency,
            request.Receiver.Name.Trim(), Trim(request.Receiver.Phone),
            Trim(request.Receiver.Email)?.ToLowerInvariant(), pricing.ChargeBasis,
            Trim(request.Pricing.NegotiationReference),
            request.SenderName.Trim(), request.PackageDescription.Trim(), request.Quantity,
            request.ActualWeightKg, request.CostWeightKg, invoice.TotalAmount,
            invoice.PreferredPaymentMethod ?? PaymentMethod.BankTransfer, request.HandoverMethod,
            Trim(request.HandoverAddress), Trim(request.HandoverCity), Trim(request.Notes));
    }

    private async Task<ResolvedOperationalEntry> ResolveUpdatedEntryAsync(OperationalManifest manifest,
        OperationalManifestEntry current, UpdateOperationalManifestEntryRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == current.InvoiceId, cancellationToken)
            ?? throw new InvalidOperationException("The generated invoice for this shipment entry is missing.");
        CargoItem? cargo = null;
        if (request.CargoItemId.HasValue)
        {
            cargo = await _context.CargoItems.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == request.CargoItemId, cancellationToken)
                ?? throw new KeyNotFoundException("Cargo/package not found.");
            if (cargo.CustomerId != current.CustomerId)
                throw new InvalidOperationException("The package belongs to a different customer.");
            if (cargo.InvoiceId.HasValue && cargo.InvoiceId != invoice.Id)
                throw new InvalidOperationException("The package belongs to a different invoice.");
        }
        ShipmentContainer? container = null;
        if (request.ShipmentContainerId.HasValue)
        {
            container = await _context.ShipmentContainers.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == request.ShipmentContainerId && x.IsActive, cancellationToken)
                ?? throw new KeyNotFoundException("Active shipment container not found.");
            if (manifest.LogisticsMethod != ManifestLogisticsMethod.Ship)
                throw new InvalidOperationException("Only Ship manifests can use a sea container.");
        }
        var movementId = request.ShipmentMovementId ?? container?.ShipmentMovementId;
        ShipmentMovement? movement = null;
        if (movementId.HasValue)
        {
            movement = await _context.ShipmentMovements.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == movementId, cancellationToken) ?? throw new KeyNotFoundException("Shipment movement not found.");
            var expected = manifest.LogisticsMethod == ManifestLogisticsMethod.Air
                ? TransportMode.Air : TransportMode.Sea;
            if (movement.Mode != expected)
                throw new InvalidOperationException("Shipment movement mode does not match the manifest Air/Ship method.");
            if (container is not null && container.ShipmentMovementId != movement.Id)
                throw new InvalidOperationException("The selected container does not belong to the selected movement.");
        }
        else if (container is not null)
            throw new InvalidOperationException("Assign the container to a shipment movement first.");
        var batchId = request.ShippingBatchId ?? cargo?.BatchId ?? movement?.ShippingBatchId ?? container?.ShippingBatchId;
        if (batchId.HasValue && !await _context.ShippingBatches.AsNoTracking().AnyAsync(x =>
                x.Id == batchId, cancellationToken)) throw new KeyNotFoundException("Shipping batch not found.");
        if (movement?.ShippingBatchId is Guid movementBatch && batchId is Guid selectedBatch &&
            movementBatch != selectedBatch)
            throw new InvalidOperationException("The selected batch does not match the shipment movement.");
        if (cargo is not null && movement is not null && (movement.Mode == TransportMode.Air || container is not null))
        {
            await _allocations.AllocateAsync(new AllocateCargoCommand(invoice.Id, new[] { cargo.Id },
                movement.Id, container?.Id, request.Notes), CurrentUserId, cancellationToken);
            cargo = await _context.CargoItems.AsNoTracking().FirstAsync(x => x.Id == cargo.Id,
                cancellationToken);
        }
        else if (cargo is not null && !cargo.InvoiceId.HasValue)
        {
            var trackedCargo = await _context.CargoItems.FirstAsync(x => x.Id == cargo.Id,
                cancellationToken);
            trackedCargo.InvoiceId = invoice.Id;
        }
        return new ResolvedOperationalEntry(cargo, batchId, movement, container,
            request.Receiver.Name.Trim(), Trim(request.Receiver.Phone),
            Trim(request.Receiver.Email)?.ToLowerInvariant(),
            request.SenderName.Trim(), request.PackageDescription.Trim(), request.Quantity,
            request.ActualWeightKg, request.CostWeightKg, request.HandoverMethod,
            Trim(request.HandoverAddress), Trim(request.HandoverCity), Trim(request.Notes));
    }

    private OperationalManifestEntry BuildEntry(Guid manifestId, ResolvedEntry value)
    {
        var entry = new OperationalManifestEntry
        {
            ManifestId = manifestId,
            AddedAt = DateTime.UtcNow,
            AddedByUserId = CurrentUserId
        };
        ApplyEntry(entry, value);
        return entry;
    }

    private static OperationalManifestChargeLine BuildChargeLine(Guid entryId,
        CalculatedChargeLine value) => new()
    {
        ManifestEntryId = entryId,
        Type = value.Type,
        Description = value.Description,
        BoxSize = value.BoxSize,
        Quantity = value.Quantity,
        UnitPrice = value.UnitPrice,
        LengthCm = value.LengthCm,
        WidthCm = value.WidthCm,
        HeightCm = value.HeightCm,
        ActualWeightKg = value.ActualWeightKg,
        VolumetricDivisor = value.VolumetricDivisor,
        VolumetricWeightKg = value.VolumetricWeightKg,
        BillableWeightKg = value.BillableWeightKg,
        LineAmount = value.LineAmount,
        SortOrder = value.SortOrder
    };

    private static void ApplyEntry(OperationalManifestEntry entry, ResolvedEntry value)
    {
        entry.CustomerId = value.Customer.Id;
        entry.InvoiceId = value.Invoice.Id;
        entry.CargoItemId = value.Cargo?.Id;
        entry.ShippingBatchId = value.BatchId;
        entry.ShipmentMovementId = value.Movement?.Id;
        entry.ShipmentContainerId = value.Container?.Id;
        entry.InvoiceNumberSnapshot = value.Invoice.InvoiceNumber;
        entry.CustomerNameSnapshot = value.Customer.FullName;
        entry.CustomerPhoneSnapshot = value.Customer.Phone;
        entry.CustomerEmailSnapshot = value.Customer.Email;
        entry.ReceiverName = value.ReceiverName;
        entry.ReceiverPhone = value.ReceiverPhone;
        entry.ReceiverEmail = value.ReceiverEmail;
        entry.TrackingNumberSnapshot = value.Cargo?.TrackingNumber;
        entry.SenderName = value.SenderName;
        entry.PackageDescription = value.PackageDescription;
        entry.Quantity = value.Quantity;
        entry.ActualWeightKg = value.ActualWeightKg;
        entry.CostWeightKg = value.CostWeightKg;
        entry.Amount = value.Amount;
        entry.Currency = value.Currency;
        entry.PaymentType = value.PaymentType;
        entry.ChargeBasis = value.ChargeBasis;
        entry.NegotiationReference = value.NegotiationReference;
        entry.HandoverMethod = value.HandoverMethod;
        entry.HandoverAddress = value.HandoverAddress;
        entry.HandoverCity = value.HandoverCity;
        entry.MovementCodeSnapshot = value.Movement?.MovementCode;
        entry.ContainerNumberSnapshot = value.Container?.ContainerNumber;
        entry.Notes = value.Notes;
    }

    private static void ApplyOperationalEntry(OperationalManifestEntry entry,
        ResolvedOperationalEntry value)
    {
        entry.CargoItemId = value.Cargo?.Id;
        entry.ShippingBatchId = value.BatchId;
        entry.ShipmentMovementId = value.Movement?.Id;
        entry.ShipmentContainerId = value.Container?.Id;
        entry.TrackingNumberSnapshot = value.Cargo?.TrackingNumber;
        entry.ReceiverName = value.ReceiverName;
        entry.ReceiverPhone = value.ReceiverPhone;
        entry.ReceiverEmail = value.ReceiverEmail;
        entry.SenderName = value.SenderName;
        entry.PackageDescription = value.PackageDescription;
        entry.Quantity = value.Quantity;
        entry.ActualWeightKg = value.ActualWeightKg;
        entry.CostWeightKg = value.CostWeightKg;
        entry.HandoverMethod = value.HandoverMethod;
        entry.HandoverAddress = value.HandoverAddress;
        entry.HandoverCity = value.HandoverCity;
        entry.MovementCodeSnapshot = value.Movement?.MovementCode;
        entry.ContainerNumberSnapshot = value.Container?.ContainerNumber;
        entry.Notes = value.Notes;
    }

    private async Task<InitialPaymentBundle> CreateInitialPaymentAsync(Invoice invoice,
        PaymentMethod method, CancellationToken cancellationToken)
    {
        var paidAt = DateTime.UtcNow;
        var settlementAccount = PaymentRouting.AccountCode(method);
        var payment = new Payment
        {
            BranchId = invoice.BranchId,
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            Reference = $"AUTO-{invoice.InvoiceNumber}",
            Amount = invoice.TotalAmount,
            Currency = invoice.Currency,
            Method = method,
            SettlementAccountCode = settlementAccount,
            Status = PaymentStatus.Confirmed,
            PaidAt = paidAt,
            Notes = "Initial payment recorded automatically when the customer shipment was created."
        };
        var financial = new FinancialTransaction
        {
            BranchId = invoice.BranchId,
            TransactionNumber = await _numbers.NextTransactionAsync(cancellationToken),
            Direction = FinancialTransactionDirection.Credit,
            Type = FinancialTransactionType.SalesPayment,
            SourceType = nameof(Payment),
            SourceId = payment.Id,
            CounterpartyId = invoice.CustomerId,
            CounterpartyName = invoice.CustomerName,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Method = payment.Method,
            SettlementAccountCode = settlementAccount,
            ExternalReference = payment.Reference,
            OccurredAt = paidAt,
            BranchCode = invoice.IssuerBranchCode,
            RecordedByUserId = CurrentUserId
        };
        var receipt = new TransactionDocument
        {
            DocumentNumber = await _numbers.NextFinancialDocumentAsync(FinancialDocumentType.Receipt,
                cancellationToken),
            Type = FinancialDocumentType.Receipt,
            FinancialTransactionId = financial.Id,
            InvoiceId = invoice.Id,
            PaymentId = payment.Id,
            IssuedAt = paidAt,
            IssuedByUserId = CurrentUserId,
            VerificationCode = DocumentSecurity.NewVerificationCode()
        };
        await _accounting.PrepareJournalAsync("Invoice", invoice.Id,
            $"Issue and settle {invoice.InvoiceNumber}", invoice.IssueDate, invoice.Currency,
            new[]
            {
                new JournalPostingLine("1100", invoice.TotalAmount, 0,
                    $"Receivable for {invoice.InvoiceNumber}"),
                new JournalPostingLine("4000", 0, invoice.TotalAmount,
                    $"Freight revenue for {invoice.InvoiceNumber}")
            }, cancellationToken);
        var paymentJournal = await _accounting.PrepareJournalAsync("Payment", payment.Id,
            $"Initial payment {payment.Reference}", paidAt, payment.Currency,
            new[]
            {
                new JournalPostingLine(settlementAccount, payment.Amount, 0,
                    $"Initial payment {payment.Reference}"),
                new JournalPostingLine("1100", 0, payment.Amount,
                    $"Settlement of {invoice.InvoiceNumber}")
            }, cancellationToken);
        financial.JournalEntryId = paymentJournal.Id;
        invoice.AmountPaid = invoice.TotalAmount;
        invoice.PaymentStatus = InvoicePaymentStatus.Paid;
        invoice.Status = InvoiceStatus.Paid;
        return new InitialPaymentBundle(payment, financial, receipt);
    }

    private async Task AwardReferralPointsAsync(Customer referrer, Customer referredCustomer,
        Invoice invoice, int points, CancellationToken cancellationToken)
    {
        var account = await _context.CustomerLoyaltyAccounts.FirstOrDefaultAsync(x =>
            x.CustomerId == referrer.Id, cancellationToken);
        if (account is null)
        {
            account = new CustomerLoyaltyAccount { CustomerId = referrer.Id };
            _context.CustomerLoyaltyAccounts.Add(account);
        }
        account.AvailablePoints += points;
        account.LifetimeEarnedPoints += points;
        _context.LoyaltyPointTransactions.Add(new LoyaltyPointTransaction
        {
            CustomerLoyaltyAccountId = account.Id,
            CustomerId = referrer.Id,
            ReferredCustomerId = referredCustomer.Id,
            InvoiceId = invoice.Id,
            Type = LoyaltyPointTransactionType.ReferralShipmentAward,
            Points = points,
            Description = $"Paid referral shipment {invoice.InvoiceNumber} for {referredCustomer.FullName}.",
            OccurredAt = DateTime.UtcNow
        });
    }

    private async Task<RouteSuggestion> ResolveRouteAsync(Guid serviceRouteId,
        CancellationToken cancellationToken)
    {
        if (serviceRouteId == Guid.Empty) throw new ArgumentException("Select a service route/direction.");
        return await (from route in _context.ServiceRoutes.AsNoTracking()
                      join origin in _context.Branches.AsNoTracking() on route.OriginBranchId equals origin.Id
                      join destination in _context.Branches.AsNoTracking()
                          on route.DestinationBranchId equals (Guid?)destination.Id into destinations
                      from destination in destinations.DefaultIfEmpty()
                      where route.Id == serviceRouteId && route.IsActive && origin.IsActive &&
                          (destination == null || destination.IsActive)
                      select new RouteSuggestion(route.Id, route.Code, route.Name, origin.CountryCode,
                          destination == null ? route.DestinationCountryCode : destination.CountryCode,
                          route.Mode == TransportMode.Sea
                              ? ManifestLogisticsMethod.Ship : ManifestLogisticsMethod.Air))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Active service route/direction not found.");
    }

    private async Task<Branch> ResolveManifestIssuerAsync(OperationalManifest manifest,
        CancellationToken cancellationToken)
    {
        if (!manifest.ServiceRouteId.HasValue)
            throw new InvalidOperationException("This shipment no longer has an active issuer route.");
        return await (from route in _context.ServiceRoutes.AsNoTracking()
                      join branch in _context.Branches.AsNoTracking() on route.OriginBranchId equals branch.Id
                      where route.Id == manifest.ServiceRouteId && route.IsActive && branch.IsActive
                      select branch).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The shipment origin branch is not active.");
    }

    private static string? ValidateHeader(CreateOperationalManifestRequest request)
    {
        var identity = ValidateIdentity(new UpdateOperationalManifestRequest(request.ShipmentId,
            request.ShipmentBookingId));
        if (identity is not null) return identity;
        if (request.ServiceRouteId == Guid.Empty) return "Select a service route/direction.";
        if (request.ManifestDate == default || request.ManifestDate.Year < 2000 ||
            request.ManifestDate > DateTime.UtcNow.AddYears(1)) return "Select a valid manifest date.";
        if (!Enum.IsDefined(request.LogisticsMethod)) return "Select Air or Ship.";
        return null;
    }

    private static string? ValidateIdentity(UpdateOperationalManifestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ShipmentId)) return "Shipment ID is required.";
        if (string.IsNullOrWhiteSpace(request.ShipmentBookingId)) return "Shipment booking ID is required.";
        if (request.ShipmentId.Trim().Length > 100) return "Shipment ID cannot exceed 100 characters.";
        if (request.ShipmentBookingId.Trim().Length > 120)
            return "Shipment booking ID cannot exceed 120 characters.";
        return null;
    }

    private static string? ValidateCreateEntry(CreateOperationalManifestEntryRequest request,
        ManifestLogisticsMethod logisticsMethod)
    {
        if (request.Customer is null || string.IsNullOrWhiteSpace(request.Customer.Name))
            return "Enter a customer name or select a matching suggestion.";
        var receiverError = ValidateReceiver(request.Receiver);
        if (receiverError is not null) return receiverError;
        if (request.Invoice is null) return "Invoice details are required.";
        if (request.Invoice.Amount <= 0) return "Invoice amount must be greater than zero.";
        if (!PaymentRouting.IsSupported(request.Invoice.PaymentMethod))
            return "Payment method must be Cash, BankTransfer, or Card.";
        var pricingError = ValidatePricing(request, logisticsMethod);
        if (pricingError is not null) return pricingError;
        if (request.ShipmentMovementId.HasValue || request.ShipmentContainerId.HasValue)
            return "Save the package first, then assign its airline movement or sea container from Edit.";
        return ValidateOperationalFields(request.SenderName, request.PackageDescription, request.Quantity,
            request.ActualWeightKg, request.CostWeightKg, request.HandoverMethod,
            request.HandoverAddress, request.HandoverCity, request.Notes);
    }

    private static string? ValidateUpdateEntry(UpdateOperationalManifestEntryRequest request)
    {
        var receiverError = ValidateReceiver(request.Receiver);
        return receiverError ?? ValidateOperationalFields(request.SenderName, request.PackageDescription, request.Quantity,
            request.ActualWeightKg, request.CostWeightKg, request.HandoverMethod,
            request.HandoverAddress, request.HandoverCity, request.Notes);
    }

    private static string? ValidateReceiver(ManifestEntryReceiverInput? receiver)
    {
        if (receiver is null || string.IsNullOrWhiteSpace(receiver.Name) ||
            receiver.Name.Trim().Length > 255)
            return "Receiver name is required.";
        if (receiver.Phone?.Trim().Length > 40 || receiver.Email?.Trim().Length > 254)
            return "One or more receiver details exceed the supported length.";
        if (!string.IsNullOrWhiteSpace(receiver.Email) &&
            !new EmailAddressAttribute().IsValid(receiver.Email.Trim()))
            return "Enter a valid receiver email address.";
        return null;
    }

    private static string? ValidatePricing(CreateOperationalManifestEntryRequest request,
        ManifestLogisticsMethod logisticsMethod)
    {
        var pricing = request.Pricing;
        if (pricing is null) return "Shipment pricing details are required.";
        if (!Enum.IsDefined(pricing.ChargeBasis)) return "Select a valid shipment charge basis.";
        if (pricing.NegotiationReference?.Trim().Length > 200)
            return "Negotiation reference cannot exceed 200 characters.";
        if (pricing.Lines is null || pricing.Lines.Count is < 1 or > 100)
            return "Add between 1 and 100 shipment charge lines.";
        if (logisticsMethod == ManifestLogisticsMethod.Air &&
            pricing.ChargeBasis == ShipmentChargeBasis.SeaBoxesAndItems)
            return "Air shipments use weight and dimensions, or a negotiated special charge.";
        if (logisticsMethod == ManifestLogisticsMethod.Ship &&
            pricing.ChargeBasis == ShipmentChargeBasis.AirWeightAndDimensions)
            return "Sea shipments use box/item prices, or a negotiated special charge.";

        foreach (var line in pricing.Lines)
        {
            if (line is null || !Enum.IsDefined(line.Type) ||
                string.IsNullOrWhiteSpace(line.Description))
                return "Every pricing line needs a valid type and description.";
            if (line.Description.Trim().Length > 1000 || line.BoxSize?.Trim().Length > 120)
                return "A pricing description or box size is too long.";
            if (line.Quantity <= 0 || line.UnitPrice <= 0)
                return "Every pricing line needs a quantity and price greater than zero.";
            var optionalNumbers = new[] { line.LengthCm, line.WidthCm, line.HeightCm,
                line.ActualWeightKg, line.VolumetricDivisor };
            if (optionalNumbers.Any(x => x.HasValue && x.Value <= 0))
                return "Dimensions, weights, and volumetric divisors must be greater than zero.";

            if (pricing.ChargeBasis == ShipmentChargeBasis.AirWeightAndDimensions)
            {
                if (line.Type != ShipmentChargeLineType.AirWeight)
                    return "Air weight-and-dimension pricing only accepts AirWeight lines.";
                if (!line.LengthCm.HasValue || !line.WidthCm.HasValue || !line.HeightCm.HasValue ||
                    !line.ActualWeightKg.HasValue || !line.VolumetricDivisor.HasValue)
                    return "Every Air line requires length, width, height, actual weight, and a volumetric divisor.";
            }
            else if (pricing.ChargeBasis == ShipmentChargeBasis.SeaBoxesAndItems)
            {
                if (line.Type is not (ShipmentChargeLineType.SeaBox or ShipmentChargeLineType.SeaItem))
                    return "Sea pricing only accepts SeaBox or SeaItem lines.";
                if (line.Type == ShipmentChargeLineType.SeaBox && string.IsNullOrWhiteSpace(line.BoxSize))
                    return "Every Sea box line requires a box size.";
                if (line.LengthCm.HasValue || line.WidthCm.HasValue || line.HeightCm.HasValue ||
                    line.ActualWeightKg.HasValue || line.VolumetricDivisor.HasValue)
                    return "Sea standard pricing is based on box sizes and items, not weight or dimensions.";
            }
            else if (line.Type != ShipmentChargeLineType.Negotiated)
                return "Negotiated pricing only accepts Negotiated charge lines.";
        }

        var calculated = CalculatePricing(pricing);
        if (Math.Abs(calculated.TotalAmount - request.Invoice.Amount) > 0.01m)
            return $"Invoice amount must equal the pricing total ({calculated.TotalAmount:0.00}).";
        if (pricing.ChargeBasis == ShipmentChargeBasis.AirWeightAndDimensions)
        {
            var lineQuantity = calculated.Lines.Sum(x => x.Quantity);
            var actualWeight = calculated.Lines.Sum(x => x.ActualWeightKg ?? 0);
            var billableWeight = calculated.Lines.Sum(x => x.BillableWeightKg ?? 0);
            if (Math.Abs(lineQuantity - request.Quantity) > 0.001m)
                return "Air package quantity must equal the total quantity in its pricing lines.";
            if (Math.Abs(actualWeight - request.ActualWeightKg) > 0.001m)
                return "Air actual weight must equal the total actual weight in its pricing lines.";
            if (Math.Abs(billableWeight - request.CostWeightKg) > 0.001m)
                return $"Air cost weight must equal the calculated billable weight ({billableWeight:0.###} kg).";
        }
        return null;
    }

    private static string? ValidateOperationalFields(string senderName, string packageDescription,
        decimal quantity, decimal actualWeightKg, decimal costWeightKg,
        ManifestHandoverMethod handoverMethod, string? handoverAddress, string? handoverCity,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(senderName)) return "Sender name is required.";
        if (string.IsNullOrWhiteSpace(packageDescription)) return "Package description is required.";
        if (senderName.Trim().Length > 255 || packageDescription.Trim().Length > 1000 ||
            handoverAddress?.Trim().Length > 1000 || handoverCity?.Trim().Length > 160 ||
            notes?.Trim().Length > 1000)
            return "One or more manifest entry fields exceed the supported length.";
        if (quantity <= 0) return "Quantity must be greater than zero.";
        if (actualWeightKg <= 0) return "Actual weight must be greater than zero.";
        if (costWeightKg < actualWeightKg)
            return "Cost weight cannot be lower than actual weight.";
        if (!Enum.IsDefined(handoverMethod)) return "Select Collection or Sent out.";
        if (handoverMethod == ManifestHandoverMethod.SentOut &&
            (string.IsNullOrWhiteSpace(handoverAddress) || string.IsNullOrWhiteSpace(handoverCity)))
            return "Sent-out entries require a delivery address and city.";
        return null;
    }

    private static object Summarize(IEnumerable<ManifestGroupRow> source,
        ManifestLogisticsMethod method)
    {
        var rows = source.Where(x => x.LogisticsMethod == method).ToList();
        var entries = rows.SelectMany(x => x.Entries).ToList();
        return new
        {
            Shipments = rows.Select(x => x.ManifestId).Distinct().Count(),
            Entries = entries.Count,
            Customers = entries.Select(x => x.CustomerId).Distinct().Count(),
            Packages = entries.Count(x => x.CargoItemId.HasValue),
            ActualWeightKg = entries.Sum(x => x.ActualWeightKg),
            CostWeightKg = entries.Sum(x => x.CostWeightKg),
            AmountsByCurrency = entries.GroupBy(x => x.Currency).OrderBy(x => x.Key)
                .Select(x => new { Currency = x.Key, Amount = x.Sum(y => y.Amount) }).ToList()
        };
    }

    private static IReadOnlyList<object> BuildDirections(IEnumerable<ManifestGroupRow> source) => source
        .GroupBy(x => new { x.OriginCountryCode, x.DestinationCountryCode })
        .OrderBy(x => x.Key.OriginCountryCode).ThenBy(x => x.Key.DestinationCountryCode)
        .Select(x => (object)new
        {
            x.Key.OriginCountryCode,
            x.Key.DestinationCountryCode,
            DirectionKey = $"{x.Key.OriginCountryCode}->{x.Key.DestinationCountryCode}",
            Routes = x.GroupBy(y => new { y.ServiceRouteId, y.RouteCode, y.RouteName,
                    y.LogisticsMethod }).OrderBy(y => y.Key.LogisticsMethod).ThenBy(y => y.Key.RouteName)
                .Select(y => new { y.Key.ServiceRouteId, y.Key.RouteCode, y.Key.RouteName,
                    y.Key.LogisticsMethod, Shipments = y.Select(z => z.ManifestId).Distinct().Count(),
                    Entries = y.Sum(z => z.Entries.Count) }).ToList(),
            Air = Summarize(x, ManifestLogisticsMethod.Air),
            Ship = Summarize(x, ManifestLogisticsMethod.Ship)
        }).ToList();

    private static PricingCalculation CalculatePricing(ManifestEntryPricingInput pricing)
    {
        var lines = new List<CalculatedChargeLine>(pricing.Lines.Count);
        for (var index = 0; index < pricing.Lines.Count; index++)
        {
            var input = pricing.Lines[index];
            decimal? volumetricWeight = null;
            decimal? billableWeight = null;
            decimal invoiceQuantity;
            string invoiceDescription;
            decimal amount;
            if (pricing.ChargeBasis == ShipmentChargeBasis.AirWeightAndDimensions)
            {
                volumetricWeight = Math.Round(input.LengthCm!.Value * input.WidthCm!.Value *
                    input.HeightCm!.Value * input.Quantity / input.VolumetricDivisor!.Value, 3,
                    MidpointRounding.AwayFromZero);
                billableWeight = Math.Max(input.ActualWeightKg!.Value, volumetricWeight.Value);
                invoiceQuantity = billableWeight.Value;
                amount = Math.Round(invoiceQuantity * input.UnitPrice, 2,
                    MidpointRounding.AwayFromZero);
                invoiceDescription = $"{input.Description.Trim()} — {input.LengthCm:0.###} × " +
                    $"{input.WidthCm:0.###} × {input.HeightCm:0.###} cm; " +
                    $"actual {input.ActualWeightKg:0.###} kg; billable {billableWeight:0.###} kg";
            }
            else
            {
                invoiceQuantity = input.Quantity;
                amount = Math.Round(invoiceQuantity * input.UnitPrice, 2,
                    MidpointRounding.AwayFromZero);
                invoiceDescription = input.Type == ShipmentChargeLineType.SeaBox
                    ? $"{input.Description.Trim()} — {input.BoxSize!.Trim()}"
                    : input.Description.Trim();
            }

            lines.Add(new CalculatedChargeLine(input.Type, input.Description.Trim(),
                Trim(input.BoxSize), input.Quantity, input.UnitPrice, input.LengthCm, input.WidthCm,
                input.HeightCm, input.ActualWeightKg, input.VolumetricDivisor, volumetricWeight,
                billableWeight, amount, index, invoiceDescription, invoiceQuantity));
        }
        return new PricingCalculation(pricing.ChargeBasis,
            Math.Round(lines.Sum(x => x.LineAmount), 2, MidpointRounding.AwayFromZero), lines);
    }

    private static void ApplyRoute(OperationalManifest manifest, RouteSuggestion route)
    {
        manifest.ServiceRouteId = route.Id;
        manifest.RouteCode = route.Code;
        manifest.RouteName = route.Name;
        manifest.OriginCountryCode = route.OriginCountryCode.Trim().ToUpperInvariant();
        manifest.DestinationCountryCode = route.DestinationCountryCode.Trim().ToUpperInvariant();
    }

    private static DateTime UtcDate(DateTime value) => DateTime.SpecifyKind(value.ToUniversalTime().Date,
        DateTimeKind.Utc);
    private static string NormalizeIdentifier(string value) => value.Trim().ToUpperInvariant();
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string JoinAddress(params string?[] parts) => string.Join(", ", parts
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
    private static string HandoverLabel(ManifestHandoverMethod method) =>
        method == ManifestHandoverMethod.HubCollection ? "Pickup" : "Delivery";
    private static string Csv(params object?[] values) => string.Join(',', values.Select(value =>
    {
        var text = value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }));

    private sealed record RouteSuggestion(Guid Id, string Code, string Name,
        string OriginCountryCode, string DestinationCountryCode, ManifestLogisticsMethod Method);
    private sealed record CustomerResolution(Customer? Customer, bool IsNew,
        ClientDuplicateReviewResult? DuplicateReview);
    private sealed record ResolvedEntry(Customer Customer, Invoice Invoice, CargoItem? Cargo,
        Guid? BatchId, ShipmentMovement? Movement, ShipmentContainer? Container, string Currency,
        string ReceiverName, string? ReceiverPhone, string? ReceiverEmail,
        ShipmentChargeBasis ChargeBasis, string? NegotiationReference,
        string SenderName, string PackageDescription, decimal Quantity, decimal ActualWeightKg,
        decimal CostWeightKg, decimal Amount, PaymentMethod PaymentType,
        ManifestHandoverMethod HandoverMethod, string? HandoverAddress, string? HandoverCity,
        string? Notes);
    private sealed record ResolvedOperationalEntry(CargoItem? Cargo, Guid? BatchId,
        ShipmentMovement? Movement, ShipmentContainer? Container, string ReceiverName,
        string? ReceiverPhone, string? ReceiverEmail, string SenderName,
        string PackageDescription, decimal Quantity, decimal ActualWeightKg, decimal CostWeightKg,
        ManifestHandoverMethod HandoverMethod, string? HandoverAddress, string? HandoverCity,
        string? Notes);
    private sealed record PricingCalculation(ShipmentChargeBasis ChargeBasis, decimal TotalAmount,
        IReadOnlyList<CalculatedChargeLine> Lines);
    private sealed record CalculatedChargeLine(ShipmentChargeLineType Type, string Description,
        string? BoxSize, decimal Quantity, decimal UnitPrice, decimal? LengthCm, decimal? WidthCm,
        decimal? HeightCm, decimal? ActualWeightKg, decimal? VolumetricDivisor,
        decimal? VolumetricWeightKg, decimal? BillableWeightKg, decimal LineAmount, int SortOrder,
        string InvoiceDescription, decimal InvoiceQuantity);
    private sealed record InitialPaymentBundle(Payment Payment,
        FinancialTransaction Transaction, TransactionDocument Receipt);
    private sealed record EntryGroupRow(Guid ManifestId, Guid EntryId, Guid CustomerId,
        Guid? CargoItemId, decimal ActualWeightKg, decimal CostWeightKg, decimal Amount,
        string Currency);
    private sealed record ManifestGroupRow(Guid ManifestId, DateTime ManifestDate,
        Guid? ServiceRouteId, string RouteCode, string RouteName, string OriginCountryCode,
        string DestinationCountryCode, ManifestLogisticsMethod LogisticsMethod,
        IReadOnlyList<EntryGroupRow> Entries);
}
