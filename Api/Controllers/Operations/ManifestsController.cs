using System.Globalization;
using System.Net;
using System.Text;
using Api.Exceptions;
using Api.Services;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

/// <summary>Shared Air or Sea loads containing only already-invoiced customer shipments.</summary>
[ApiController]
[Authorize(Policy = "OperationsTeam")]
[Route("api/operations/manifests")]
public sealed class ManifestsController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    private readonly ICompanyDocumentNumberGenerator _numbers;
    private readonly IClientCommunicationService _communications;
    private readonly IOfficeDocumentService _documents;

    public ManifestsController(ApplicationDbContext context, IActiveBranchContext branches,
        ICompanyDocumentNumberGenerator numbers, IClientCommunicationService communications,
        IOfficeDocumentService documents)
    {
        _context = context;
        _branches = branches;
        _numbers = numbers;
        _communications = communications;
        _documents = documents;
    }

    [HttpPost]
    public async Task<ActionResult<ManifestResponse>> Create(CreateManifestRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var key = RequireIdempotencyKey();
        ValidateCreate(request, branch);
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "manifest.create" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null) return Ok(await MapManifestAsync(prior.ResourceId, false, cancellationToken));
        var name = NormalizeName(request.ShipmentName);
        if (await _context.OperationalManifests.AnyAsync(x => x.ShipmentId == name, cancellationToken))
            throw new ApiProblemException(409, "SHIPMENT_NAME_IN_USE", "Shipment name is already in use",
                "Choose a unique shipment name.");
        var destinationCountry = CountryCode(request.Destination.CountryCode);
        ServiceRoute? serviceRoute = null;
        if (request.ServiceRouteId.HasValue)
        {
            serviceRoute = await _context.ServiceRoutes.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == request.ServiceRouteId && x.OriginBranchId == branch.BranchId && x.IsActive,
                cancellationToken) ?? throw Rule("SERVICE_ROUTE_NOT_FOUND",
                    "The selected active service route does not belong to this origin hub.");
            if (InternalMethod(request.Method) != (serviceRoute.Mode == TransportMode.Sea
                    ? ManifestLogisticsMethod.Ship : ManifestLogisticsMethod.Air) ||
                CountryCode(serviceRoute.DestinationCountryCode) != destinationCountry ||
                !CitiesCompatible(serviceRoute.DestinationCity, request.Destination.City))
                throw Rule("SERVICE_ROUTE_MISMATCH",
                    "The selected route does not match the manifest method and destination.");
        }
        var manifest = new OperationalManifest
        {
            BranchId = branch.BranchId!.Value,
            ManifestNumber = await _numbers.NextOperationalManifestAsync(cancellationToken),
            ShipmentId = name,
            ShipmentBookingId = string.Concat("PENDING-", Guid.NewGuid().ToString("N")),
            BookingReference = NullIfWhiteSpace(request.BookingReference),
            ServiceRouteId = serviceRoute?.Id,
            ManifestDate = UtcDate(request.ManifestDate),
            LogisticsMethod = InternalMethod(request.Method),
            OriginCountryCode = branch.CountryCode!, OriginCity = branch.City!,
            DestinationCountryCode = destinationCountry,
            DestinationCity = request.Destination.City.Trim(),
            RouteCode = serviceRoute?.Code ?? RouteCode(branch.CountryCode!, branch.City!,
                destinationCountry, request.Destination.City),
            RouteName = serviceRoute?.Name ??
                string.Concat(branch.City, " → ", request.Destination.City.Trim()),
            CreatedByUserId = CurrentUserId
        };
        _context.OperationalManifests.Add(manifest);
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            ActorUserId = CurrentUserId, Operation = "manifest.create", Key = key,
            ResourceType = nameof(OperationalManifest), ResourceId = manifest.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapManifestAsync(manifest.Id, false, cancellationToken);
        Response.Headers.ETag = Quote(response.RowVersion);
        return CreatedAtAction(nameof(Get), new { manifestId = manifest.Id }, response);
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search,
        [FromQuery] CustomerShipmentMethod? method, [FromQuery] string? origin,
        [FromQuery] string? destination, [FromQuery] string? route,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? shipmentName, [FromQuery] string? carrier,
        [FromQuery] string? invoiceNumber, [FromQuery] Guid? clientId,
        [FromQuery] bool includeLines = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.OperationalManifests.AsNoTracking().Where(x =>
            branch.IsAllBranches || x.BranchId == branch.BranchId);
        if (method.HasValue) query = query.Where(x => x.LogisticsMethod == InternalMethod(method.Value));
        if (!string.IsNullOrWhiteSpace(origin))
        {
            var value = origin.Trim().ToLowerInvariant();
            query = query.Where(x => x.OriginCountryCode.ToLower().Contains(value) ||
                x.OriginCity.ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(destination))
        {
            var value = destination.Trim().ToLowerInvariant();
            query = query.Where(x => x.DestinationCountryCode.ToLower().Contains(value) ||
                x.DestinationCity.ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(route))
        {
            var value = route.Trim().ToLowerInvariant();
            query = query.Where(x => x.RouteCode.ToLower() == value || x.RouteName.ToLower() == value);
        }
        if (from.HasValue) query = query.Where(x => x.ManifestDate >= UtcDate(from.Value));
        if (to.HasValue) query = query.Where(x => x.ManifestDate < UtcDate(to.Value).AddDays(1));
        if (!string.IsNullOrWhiteSpace(shipmentName))
        {
            var value = shipmentName.Trim().ToLowerInvariant();
            query = query.Where(x => x.ShipmentId.ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(carrier))
        {
            var value = carrier.Trim().ToLowerInvariant();
            query = query.Where(x => x.CarrierName != null && x.CarrierName.ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var value = invoiceNumber.Trim().ToUpperInvariant();
            query = query.Where(x => _context.OperationalManifestEntries.Any(line =>
                line.ManifestId == x.Id && line.InvoiceNumberSnapshot == value));
        }
        if (clientId.HasValue) query = query.Where(x => _context.OperationalManifestEntries.Any(line =>
            line.ManifestId == x.Id && line.CustomerId == clientId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.ManifestNumber.ToLower().Contains(value) ||
                x.ShipmentId.ToLower().Contains(value) ||
                (x.BookingReference != null && x.BookingReference.ToLower().Contains(value)) ||
                x.RouteName.ToLower().Contains(value) ||
                _context.OperationalManifestEntries.Any(line => line.ManifestId == x.Id &&
                    (line.InvoiceNumberSnapshot.ToLower().Contains(value) ||
                     line.CustomerNameSnapshot.ToLower().Contains(value) ||
                     line.SenderName.ToLower().Contains(value))));
        }
        var result = await query.OrderBy(x => x.LogisticsMethod).ThenBy(x => x.OriginCountryCode)
            .ThenBy(x => x.OriginCity).ThenBy(x => x.DestinationCountryCode)
            .ThenBy(x => x.DestinationCity).ThenByDescending(x => x.ManifestDate)
            .ThenByDescending(x => x.Id).ToPagedResponseAsync(page, pageSize, true, cancellationToken);
        var lineCounts = await _context.OperationalManifestEntries.AsNoTracking()
            .Where(x => result.Items.Select(m => m.Id).Contains(x.ManifestId))
            .GroupBy(x => x.ManifestId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        Dictionary<Guid, IReadOnlyList<ManifestLineResponse>> linesByManifest = [];
        if (includeLines)
        {
            var pageIds = result.Items.Select(x => x.Id).ToList();
            var rows = await (from line in _context.OperationalManifestEntries.AsNoTracking()
                join shipment in _context.CustomerShipments.AsNoTracking()
                    on line.CustomerShipmentId equals shipment.Id
                join client in _context.Customers.AsNoTracking() on line.CustomerId equals client.Id
                join user in _context.UserAccounts.AsNoTracking() on line.AddedByUserId equals user.Id
                where pageIds.Contains(line.ManifestId)
                orderby line.AddedAt, line.Id
                select new
                {
                    line.ManifestId,
                    Item = new ManifestLineResponse(line.Id, line.RowVersion, shipment.Id,
                        line.InvoiceNumberSnapshot, client.Id, client.ClientNumber,
                        line.CustomerNameSnapshot, line.SenderName, line.ReceiverName,
                        line.PackageDescription, line.Contents, (int)line.Quantity,
                        line.ActualWeightKg, line.CostWeightKg,
                        line.HandoverMethod == ManifestHandoverMethod.HubCollection
                            ? ShipmentHandoverMethod.Pickup : ShipmentHandoverMethod.Delivery,
                        line.HandoverCity ?? shipment.DestinationCityOrState, line.HandoverAddress,
                        line.AddedAt, line.LastModifiedAt)
                    {
                        ManifestId = line.ManifestId, InvoiceId = line.InvoiceId,
                        CustomerPhone = line.CustomerPhoneSnapshot,
                        CustomerEmail = NullIfWhiteSpace(line.CustomerEmailSnapshot),
                        Amount = line.Amount, Currency = line.Currency, PaymentMethod = line.PaymentType,
                        ChargeBasis = line.ChargeBasis,
                        NegotiationReference = line.NegotiationReference, Notes = line.Notes,
                        AddedByUserId = line.AddedByUserId, AddedBy = user.FullName
                    }
                }).ToListAsync(cancellationToken);
            linesByManifest = rows.GroupBy(x => x.ManifestId)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<ManifestLineResponse>)x.Select(y => y.Item).ToList());
        }
        var items = result.Items.Select(x =>
        {
            var response = MapHeader(x, lineCounts.GetValueOrDefault(x.Id));
            return includeLines
                ? response with { Lines = linesByManifest.GetValueOrDefault(x.Id) ?? [] }
                : response;
        }).ToList();
        return Ok(new PagedResponse<ManifestResponse>(items, result.Page, result.PageSize,
            result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ManifestSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var manifests = _context.OperationalManifests.AsNoTracking().Where(x =>
            branch.IsAllBranches || x.BranchId == branch.BranchId);
        var ids = manifests.Select(x => x.Id);
        var lineCount = await _context.OperationalManifestEntries.AsNoTracking()
            .CountAsync(x => ids.Contains(x.ManifestId), cancellationToken);
        return Ok(new ManifestSummaryResponse(
            await manifests.CountAsync(x => x.LogisticsMethod == ManifestLogisticsMethod.Air, cancellationToken),
            await manifests.CountAsync(x => x.LogisticsMethod == ManifestLogisticsMethod.Ship, cancellationToken),
            await manifests.CountAsync(x => !_context.OperationalManifestEntries.Any(l => l.ManifestId == x.Id),
                cancellationToken), lineCount));
    }

    [HttpGet("{manifestId:guid}")]
    public async Task<ActionResult<ManifestResponse>> Get(Guid manifestId,
        CancellationToken cancellationToken)
    {
        var response = await MapManifestAsync(manifestId, true, cancellationToken);
        Response.Headers.ETag = Quote(response.RowVersion);
        return Ok(response);
    }

    [HttpDelete("{manifestId:guid}")]
    public async Task<IActionResult> Delete(Guid manifestId, [FromQuery] string reason,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        if (string.IsNullOrWhiteSpace(reason))
            throw Rule("DELETE_REASON_REQUIRED", "A manifest deletion reason is required.");
        var manifest = await _context.OperationalManifests.FirstOrDefaultAsync(x => x.Id == manifestId &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Manifest not found.");
        CheckVersion(manifest.RowVersion, version);
        if (await _context.OperationalManifestEntries.AsNoTracking().AnyAsync(x =>
            x.ManifestId == manifestId, cancellationToken))
            throw new ApiProblemException(409, "MANIFEST_NOT_EMPTY", "Manifest still contains shipments",
                "Remove or reassign every customer shipment before deleting the manifest.");
        manifest.Notes = string.Concat("Deleted: ", reason.Trim());
        _context.OperationalManifests.Remove(manifest);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("{manifestId:guid}")]
    public async Task<ActionResult<ManifestResponse>> Update(Guid manifestId, UpdateManifestRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        if (string.IsNullOrWhiteSpace(request.ShipmentName))
            throw Rule("SHIPMENT_NAME_REQUIRED", "Shipment name is required.");
        var manifest = await _context.OperationalManifests.FirstOrDefaultAsync(x => x.Id == manifestId &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Manifest not found.");
        CheckVersion(manifest.RowVersion, version);
        var name = NormalizeName(request.ShipmentName);
        if (await _context.OperationalManifests.AsNoTracking().AnyAsync(x => x.Id != manifest.Id &&
            x.ShipmentId == name, cancellationToken)) throw new ApiProblemException(409,
                "SHIPMENT_NAME_IN_USE", "Shipment name is already in use", "Choose a unique shipment name.");
        manifest.ShipmentId = name;
        manifest.BookingReference = NullIfWhiteSpace(request.BookingReference);
        manifest.LastEditedByUserId = CurrentUserId;
        manifest.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapManifestAsync(manifestId, false, cancellationToken);
        Response.Headers.ETag = Quote(response.RowVersion);
        return Ok(response);
    }

    [HttpGet("invoice-lookup")]
    public async Task<ActionResult<ManifestInvoiceLookupResponse>> InvoiceLookup([FromQuery] string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var number = invoiceNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(number)) throw Rule("INVOICE_NUMBER_REQUIRED", "Invoice number is required.");
        var result = await (from shipment in _context.CustomerShipments.AsNoTracking()
            join client in _context.Customers.AsNoTracking() on shipment.CustomerId equals client.Id
            where shipment.InvoiceNumber == number && (branch.IsAllBranches || shipment.BranchId == branch.BranchId)
            select new { Shipment = shipment, Client = client }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Customer shipment invoice was not found.");
        var attachment = await (from line in _context.OperationalManifestEntries.AsNoTracking()
            join manifest in _context.OperationalManifests.AsNoTracking() on line.ManifestId equals manifest.Id
            where line.CustomerShipmentId == result.Shipment.Id
            select new { manifest.Id, manifest.ManifestNumber, manifest.ShipmentId }).SingleOrDefaultAsync(cancellationToken);
        return Ok(new ManifestInvoiceLookupResponse(result.Shipment.InvoiceNumber, result.Shipment.Id,
            result.Client.Id, result.Client.ClientNumber, result.Client.FullName, result.Shipment.ReceiverName,
            result.Shipment.Method, result.Shipment.Description, result.Shipment.Contents,
            result.Shipment.PieceCount, result.Shipment.ActualWeightKg, result.Shipment.ChargeableWeightKg,
            result.Shipment.HandoverMethod, result.Shipment.DestinationCountryCode,
            result.Shipment.DestinationCityOrState, result.Shipment.DeliveryAddress,
            attachment?.Id, attachment?.ManifestNumber, attachment?.ShipmentId));
    }

    [HttpPost("{manifestId:guid}/lines")]
    public async Task<ActionResult<ManifestLineResponse>> AddLine(Guid manifestId,
        AddManifestLineRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var key = RequireIdempotencyKey();
        if (string.IsNullOrWhiteSpace(request.InvoiceNumber) || string.IsNullOrWhiteSpace(request.SenderName))
            throw Rule("MANIFEST_LINE_DETAILS_REQUIRED", "Invoice number and sender name are required.");
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "manifest.line.add" && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null) return Ok(await MapLineAsync(prior.ResourceId, cancellationToken));
        var manifest = await _context.OperationalManifests.FirstOrDefaultAsync(x => x.Id == manifestId &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Manifest not found.");
        var number = request.InvoiceNumber.Trim().ToUpperInvariant();
        var data = await (from shipment in _context.CustomerShipments
            join invoice in _context.Invoices on shipment.InvoiceId equals invoice.Id
            join client in _context.Customers on shipment.CustomerId equals client.Id
            where shipment.InvoiceNumber == number && shipment.BranchId == branch.BranchId
            select new { Shipment = shipment, Invoice = invoice, Client = client })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("Customer shipment was not found.");
        if (InternalMethod(data.Shipment.Method) != manifest.LogisticsMethod)
            throw Rule("MANIFEST_METHOD_MISMATCH", "Air and Sea customer shipments cannot be mixed.");
        EnsureRouteCompatible(manifest, data.Shipment);
        if (data.Shipment.MovementState == CustomerShipmentMovementState.Cancelled)
            throw Rule("CANCELLED_SHIPMENT", "A cancelled customer shipment cannot be attached.");
        var existing = await _context.OperationalManifestEntries.AsNoTracking().FirstOrDefaultAsync(x =>
            x.CustomerShipmentId == data.Shipment.Id || x.InvoiceId == data.Invoice.Id, cancellationToken);
        if (existing is not null) throw new ApiProblemException(409, "SHIPMENT_ALREADY_ASSIGNED",
            "Customer shipment is already on a manifest", "Use the guarded reassign-line operation.",
            extensions: new Dictionary<string, object?> { ["manifestId"] = existing.ManifestId,
                ["lineId"] = existing.Id });
        var actual = request.ActualWeightKg ?? data.Shipment.ActualWeightKg;
        if (!actual.HasValue || actual <= 0)
            throw Rule("ACTUAL_WEIGHT_REQUIRED", "Enter the checked actual weight before attaching this shipment.");
        var chargeable = request.ChargeableWeightKg ?? data.Shipment.ChargeableWeightKg ?? actual;
        if (chargeable < actual) throw Rule("INVALID_CHARGEABLE_WEIGHT",
            "Chargeable weight cannot be below actual weight.");
        var line = new OperationalManifestEntry
        {
            ManifestId = manifest.Id, CustomerShipmentId = data.Shipment.Id,
            CustomerId = data.Client.Id, InvoiceId = data.Invoice.Id,
            InvoiceNumberSnapshot = data.Invoice.InvoiceNumber, CustomerNameSnapshot = data.Client.FullName,
            CustomerPhoneSnapshot = data.Client.Phone, CustomerEmailSnapshot = data.Client.Email ?? string.Empty,
            ReceiverName = data.Shipment.ReceiverName, SenderName = PersonName(request.SenderName),
            PackageDescription = data.Shipment.Description, Contents = data.Shipment.Contents,
            Quantity = data.Shipment.PieceCount, ActualWeightKg = actual.Value, CostWeightKg = chargeable.Value,
            Amount = data.Invoice.TotalAmount, Currency = data.Invoice.Currency,
            PaymentType = data.Invoice.PreferredPaymentMethod ?? PaymentMethod.BankTransfer,
            ChargeBasis = ChargeBasis(data.Shipment.PricingMethod),
            NegotiationReference = data.Shipment.NegotiatedReason,
            HandoverMethod = data.Shipment.HandoverMethod == ShipmentHandoverMethod.Pickup
                ? ManifestHandoverMethod.HubCollection : ManifestHandoverMethod.SentOut,
            HandoverAddress = data.Shipment.DeliveryAddress,
            HandoverCity = data.Shipment.DestinationCityOrState,
            AddedAt = DateTime.UtcNow, AddedByUserId = CurrentUserId
        };
        data.Shipment.MovementState = CustomerShipmentMovementState.AssignedToManifest;
        data.Shipment.RowVersion++;
        _context.OperationalManifestEntries.Add(line);
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "manifest.line.add", Key = key, ResourceType = nameof(OperationalManifestEntry),
            ResourceId = line.Id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapLineAsync(line.Id, cancellationToken);
        Response.Headers.ETag = Quote(response.RowVersion);
        return CreatedAtAction(nameof(GetLine), new { manifestId, lineId = line.Id }, response);
    }

    [HttpGet("{manifestId:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<ManifestLineResponse>> GetLine(Guid manifestId, Guid lineId,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var allowed = await _context.OperationalManifestEntries.AsNoTracking().AnyAsync(x => x.Id == lineId &&
            x.ManifestId == manifestId && (branch.IsAllBranches ||
                _context.OperationalManifests.Any(m => m.Id == manifestId && m.BranchId == branch.BranchId)),
            cancellationToken);
        if (!allowed) throw new KeyNotFoundException("Manifest line not found.");
        var response = await MapLineAsync(lineId, cancellationToken);
        Response.Headers.ETag = Quote(response.RowVersion);
        return Ok(response);
    }

    [HttpPatch("{manifestId:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<ManifestLineResponse>> UpdateLine(Guid manifestId, Guid lineId,
        UpdateManifestLineRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        ValidateLineUpdate(request);
        var data = await (from value in _context.OperationalManifestEntries
            join manifest in _context.OperationalManifests on value.ManifestId equals manifest.Id
            where value.Id == lineId && value.ManifestId == manifestId && manifest.BranchId == branch.BranchId
            select new { Line = value, Manifest = manifest }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Manifest line not found.");
        var line = data.Line;
        CheckVersion(line.RowVersion, version);
        var shipment = await _context.CustomerShipments.FirstAsync(x => x.Id == line.CustomerShipmentId,
            cancellationToken);
        EnsureRouteCompatible(data.Manifest, shipment.OriginCountryCode, shipment.OriginCity,
            shipment.DestinationCountryCode, request.DestinationCity);
        line.SenderName = PersonName(request.SenderName);
        line.ReceiverName = PersonName(request.ReceiverName);
        line.PackageDescription = request.Description.Trim(); line.Contents = NullIfWhiteSpace(request.Contents);
        line.Quantity = request.Pieces; line.ActualWeightKg = request.ActualWeightKg;
        line.CostWeightKg = request.ChargeableWeightKg ?? request.ActualWeightKg;
        line.HandoverMethod = request.HandoverMethod == ShipmentHandoverMethod.Pickup
            ? ManifestHandoverMethod.HubCollection : ManifestHandoverMethod.SentOut;
        line.HandoverCity = request.DestinationCity.Trim();
        line.HandoverAddress = request.HandoverMethod == ShipmentHandoverMethod.Delivery
            ? request.DeliveryAddress!.Trim() : NullIfWhiteSpace(request.DeliveryAddress);
        line.Notes = string.Concat("Last edit reason: ", request.Reason.Trim()); line.RowVersion++;
        shipment.ReceiverName = line.ReceiverName; shipment.Description = line.PackageDescription;
        shipment.Contents = line.Contents; shipment.PieceCount = request.Pieces;
        shipment.ActualWeightKg = request.ActualWeightKg;
        shipment.ChargeableWeightKg = request.ChargeableWeightKg;
        shipment.HandoverMethod = request.HandoverMethod;
        shipment.DestinationCityOrState = request.DestinationCity.Trim();
        shipment.DeliveryAddress = line.HandoverAddress; shipment.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapLineAsync(lineId, cancellationToken);
        Response.Headers.ETag = Quote(response.RowVersion);
        return Ok(response);
    }

    [HttpDelete("{manifestId:guid}/lines/{lineId:guid}")]
    public async Task<IActionResult> DeleteLine(Guid manifestId, Guid lineId, [FromQuery] string reason,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        if (string.IsNullOrWhiteSpace(reason)) throw Rule("REMOVE_REASON_REQUIRED", "A removal reason is required.");
        var line = await (from value in _context.OperationalManifestEntries
            join manifest in _context.OperationalManifests on value.ManifestId equals manifest.Id
            where value.Id == lineId && value.ManifestId == manifestId && manifest.BranchId == branch.BranchId
            select value).SingleOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("Manifest line not found.");
        CheckVersion(line.RowVersion, version);
        var shipment = await _context.CustomerShipments.FirstOrDefaultAsync(x => x.Id == line.CustomerShipmentId,
            cancellationToken);
        if (shipment is not null)
        { shipment.MovementState = CustomerShipmentMovementState.ReadyForManifest; shipment.RowVersion++; }
        line.Notes = string.Concat("Removed: ", reason.Trim());
        _context.OperationalManifestEntries.Remove(line);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{manifestId:guid}/reassign-line")]
    public async Task<ActionResult<ManifestLineResponse>> ReassignLine(Guid manifestId,
        ReassignManifestLineRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        if (!request.Confirm || string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("REASSIGN_CONFIRMATION_REQUIRED", "Confirm reassignment and provide a reason.");
        var target = await _context.OperationalManifests.FirstOrDefaultAsync(x => x.Id == manifestId &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Target manifest not found.");
        var number = request.InvoiceNumber.Trim().ToUpperInvariant();
        var line = await (from value in _context.OperationalManifestEntries
                          join source in _context.OperationalManifests on value.ManifestId equals source.Id
                          where value.InvoiceNumberSnapshot == number && source.BranchId == branch.BranchId
                          select value).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Manifest line not found.");
        CheckVersion(line.RowVersion, version);
        var shipment = await _context.CustomerShipments.AsNoTracking().SingleAsync(x =>
            x.Id == line.CustomerShipmentId && x.BranchId == branch.BranchId, cancellationToken);
        if (InternalMethod(shipment.Method) != target.LogisticsMethod)
            throw Rule("MANIFEST_METHOD_MISMATCH", "Air and Sea customer shipments cannot be mixed.");
        if (line.ManifestId == target.Id)
            throw Rule("SHIPMENT_ALREADY_ON_TARGET_MANIFEST",
                "The customer shipment is already attached to this manifest.");
        EnsureRouteCompatible(target, shipment);
        line.ManifestId = target.Id; line.Notes = string.Concat("Reassigned: ", request.Reason.Trim());
        line.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapLineAsync(line.Id, cancellationToken);
        Response.Headers.ETag = Quote(response.RowVersion);
        return Ok(response);
    }

    [HttpPut("{manifestId:guid}/carrier-connection")]
    public async Task<ActionResult<ManifestResponse>> ConnectCarrier(Guid manifestId,
        UpdateManifestCarrierRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        if (string.IsNullOrWhiteSpace(request.CarrierName) || string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("CARRIER_DETAILS_REQUIRED", "Carrier name and change reason are required.");
        var manifest = await _context.OperationalManifests.FirstOrDefaultAsync(x => x.Id == manifestId &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Manifest not found.");
        CheckVersion(manifest.RowVersion, version);
        manifest.CarrierName = request.CarrierName.Trim();
        manifest.CarrierReference = NullIfWhiteSpace(request.CarrierReference);
        manifest.TrackingProvider = NullIfWhiteSpace(request.TrackingProvider);
        manifest.ContainerOrFlightNumber = NullIfWhiteSpace(request.ContainerOrFlightNumber);
        manifest.BillOfLadingNumber = NullIfWhiteSpace(request.BillOfLadingNumber);
        manifest.FlightNumber = NullIfWhiteSpace(request.FlightNumber);
        manifest.VesselName = NullIfWhiteSpace(request.VesselName);
        manifest.VoyageNumber = NullIfWhiteSpace(request.VoyageNumber);
        manifest.Notes = NullIfWhiteSpace(request.Notes);
        manifest.LastEditedByUserId = CurrentUserId; manifest.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        var response = await MapManifestAsync(manifestId, false, cancellationToken);
        Response.Headers.ETag = Quote(response.RowVersion);
        return Ok(response);
    }

    [HttpGet("{manifestId:guid}/print")]
    [HttpGet("/api/documents/manifests/{manifestId:guid}/print")]
    public async Task<IActionResult> Print(Guid manifestId, CancellationToken cancellationToken)
    {
        var manifest = await MapManifestAsync(manifestId, true, cancellationToken);
        var table = new OfficeSheet("Packages",
            ["Invoice", "Customer / owner", "Sender", "Receiver", "Pieces", "Actual kg",
                "Handover", "Destination", "Delivery address"],
            (manifest.Lines ?? []).Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.InvoiceNumber), new(x.ClientName), new(x.SenderName), new(x.ReceiverName),
                new(x.Pieces, OfficeCellKind.Integer), new(x.ActualWeightKg, OfficeCellKind.Number),
                new(x.HandoverMethod.ToString()), new(x.DestinationCity),
                new(x.HandoverMethod == ShipmentHandoverMethod.Delivery ? x.DeliveryAddress ?? "" : "")
            ]).ToList());
        var branding = await DocumentBranding(manifest.BranchId, cancellationToken);
        var document = new BusinessDocument("Operations-safe manifest", manifest.ManifestNumber,
            branding.CompanyName,
        [
            Pair("Issuing hub", branding.HubName),
            Pair("Hub address", branding.HubAddress),
            Pair("Shipment", manifest.ShipmentName),
            Pair("Booking reference", manifest.BookingReference ?? "Pending"),
            Pair("Date", manifest.ManifestDate.ToString("yyyy-MM-dd")),
            Pair("Method", manifest.Method.ToString()),
            Pair("Route", $"{manifest.OriginCity} → {manifest.DestinationCity}"),
            Pair("Carrier", manifest.CarrierName ?? "Not assigned"),
            Pair("Container / flight", manifest.ContainerOrFlightNumber ?? "Not assigned"),
            Pair("Generated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"))
        ], table, [],
            Footer: "Operations copy. Private commercial and package-detail fields are intentionally excluded.");
        var rendered = _documents.Render(document, BusinessDocumentFormat.Pdf);
        return File(rendered.Content, rendered.ContentType, $"{manifest.ManifestNumber}-operations.pdf");
    }

    [HttpGet("{manifestId:guid}/report")]
    [HttpGet("/api/documents/manifests/{manifestId:guid}/report")]
    public async Task<IActionResult> Report(Guid manifestId,
        [FromQuery] BusinessDocumentFormat format = BusinessDocumentFormat.Xlsx,
        CancellationToken cancellationToken = default)
    {
        var manifest = await MapManifestAsync(manifestId, false, cancellationToken);
        var entries = await _context.OperationalManifestEntries.AsNoTracking()
            .Where(x => x.ManifestId == manifestId).OrderBy(x => x.AddedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var addedByIds = entries.Select(x => x.AddedByUserId).Distinct().ToList();
        var addedBy = await _context.UserAccounts.AsNoTracking().Where(x => addedByIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var table = new OfficeSheet("Manifest packages",
            ["Invoice", "Customer / owner", "Sender", "Receiver", "Description", "Contents",
                "Pieces", "Actual kg", "Chargeable kg", "Amount", "Currency", "Payment method",
                "Charge basis", "Negotiation reference", "Handover", "Destination",
                "Delivery address", "Internal notes", "Added by", "Added date"],
            entries.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.InvoiceNumberSnapshot), new(x.CustomerNameSnapshot), new(x.SenderName),
                new(x.ReceiverName), new(x.PackageDescription), new(x.Contents ?? ""),
                new(x.Quantity, OfficeCellKind.Number), new(x.ActualWeightKg, OfficeCellKind.Number),
                new(x.CostWeightKg, OfficeCellKind.Number), new(x.Amount, OfficeCellKind.Money),
                new(x.Currency), new(x.PaymentType.ToString()), new(x.ChargeBasis.ToString()),
                new(x.NegotiationReference ?? ""), new(x.HandoverMethod.ToString()),
                new(x.HandoverCity ?? ""), new(x.HandoverAddress ?? ""), new(x.Notes ?? ""),
                new(addedBy.GetValueOrDefault(x.AddedByUserId) ?? x.AddedByUserId.ToString()),
                new(x.AddedAt, OfficeCellKind.DateTime)
            ]).ToList());
        var branding = await DocumentBranding(manifest.BranchId, cancellationToken);
        var document = new BusinessDocument("Controlled manifest report", manifest.ManifestNumber,
            branding.CompanyName,
        [
            Pair("Issuing hub", branding.HubName),
            Pair("Hub address", branding.HubAddress),
            Pair("Shipment", manifest.ShipmentName),
            Pair("Booking reference", manifest.BookingReference ?? "Pending"),
            Pair("Date", manifest.ManifestDate.ToString("yyyy-MM-dd")),
            Pair("Method", manifest.Method.ToString()),
            Pair("Route", $"{manifest.OriginCity} → {manifest.DestinationCity}"),
            Pair("Carrier", manifest.CarrierName ?? "Not assigned"),
            Pair("Carrier reference", manifest.CarrierReference ?? "Not assigned"),
            Pair("Container / flight", manifest.ContainerOrFlightNumber ?? "Not assigned"),
            Pair("Bill of lading", manifest.BillOfLadingNumber ?? "Not assigned"),
            Pair("Flight", manifest.FlightNumber ?? "Not assigned"),
            Pair("Vessel / voyage", string.Join(" / ", new[] { manifest.VesselName, manifest.VoyageNumber }
                .Where(x => !string.IsNullOrWhiteSpace(x))).Trim(' ', '/') is { Length: > 0 } vessel
                    ? vessel : "Not assigned"),
            Pair("Generated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"))
        ], table, [],
            Footer: "Controlled internal report. This file contains commercial and package details.");
        var rendered = _documents.Render(document, format);
        return File(rendered.Content, rendered.ContentType,
            $"{manifest.ManifestNumber}-controlled-report.{rendered.FileExtension}");
    }

    [HttpPost("{manifestId:guid}/email")]
    public async Task<ActionResult> Email(Guid manifestId, EmailManifestRequest request,
        CancellationToken cancellationToken)
    {
        await _branches.ResolveAsync(true, cancellationToken);
        var key = RequireIdempotencyKey();
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            throw Rule("EMAIL_DETAILS_REQUIRED", "Email subject and body are required.");
        var manifest = await MapManifestAsync(manifestId, true, cancellationToken);
        var prior = await _context.IdempotencyRecords.AsNoTracking().AnyAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "manifest.email" && x.Key == key &&
            x.ResourceId == manifestId && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior)
            return Accepted(new { manifestId, replayed = true, deliveryQueued = true });
        var clientIds = (manifest.Lines ?? []).Select(x => x.ClientId).Distinct().ToList();
        var deliverable = await _context.Customers.AsNoTracking().Where(x => clientIds.Contains(x.Id) && x.Email != null)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var clientId in deliverable)
        {
            await _communications.SendAsync(clientId, CommunicationType.Manifest,
                request.Subject.Trim(), request.Body.Trim(), nameof(OperationalManifest), manifestId,
                request.AttachmentDocumentIds ?? [], new { manifest.ManifestNumber, manifest.ShipmentName,
                    manifest.Method, Route = $"{manifest.OriginCity} → {manifest.DestinationCity}" }, cancellationToken);
            sent++;
        }
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = "manifest.email", Key = key, ResourceType = nameof(OperationalManifest),
            ResourceId = manifestId, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken);
        return Accepted(new { manifestId, recipients = sent, deliveryQueued = true });
    }

    [HttpGet("{manifestId:guid}/history")]
    public async Task<ActionResult> History(Guid manifestId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        if (!await _context.OperationalManifests.AsNoTracking().AnyAsync(x => x.Id == manifestId &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken))
            throw new KeyNotFoundException("Manifest not found.");
        var lineIds = await _context.OperationalManifestEntries.AsNoTracking().Where(x => x.ManifestId == manifestId)
            .Select(x => x.Id.ToString()).ToListAsync(cancellationToken);
        lineIds.Add(manifestId.ToString());
        var query = _context.AuditLogs.AsNoTracking().Where(x => lineIds.Contains(x.EntityId));
        return Ok(await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, true, cancellationToken));
    }

    private async Task<ManifestResponse> MapManifestAsync(Guid id, bool includeLines,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var manifest = await _context.OperationalManifests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken)
            ?? throw new KeyNotFoundException("Manifest not found.");
        IReadOnlyList<ManifestLineResponse>? lines = null;
        var lineQuery = from line in _context.OperationalManifestEntries.AsNoTracking()
            join shipment in _context.CustomerShipments.AsNoTracking()
                on line.CustomerShipmentId equals shipment.Id
            join client in _context.Customers.AsNoTracking() on line.CustomerId equals client.Id
            join user in _context.UserAccounts.AsNoTracking() on line.AddedByUserId equals user.Id
            where line.ManifestId == id
            orderby line.AddedAt, line.Id
            select new ManifestLineResponse(line.Id, line.RowVersion, shipment.Id,
                line.InvoiceNumberSnapshot, client.Id, client.ClientNumber,
                line.CustomerNameSnapshot, line.SenderName, line.ReceiverName,
                line.PackageDescription, line.Contents, (int)line.Quantity,
                line.ActualWeightKg, line.CostWeightKg,
                line.HandoverMethod == ManifestHandoverMethod.HubCollection
                    ? ShipmentHandoverMethod.Pickup : ShipmentHandoverMethod.Delivery,
                line.HandoverCity ?? shipment.DestinationCityOrState, line.HandoverAddress,
                line.AddedAt, line.LastModifiedAt)
            {
                ManifestId = line.ManifestId, InvoiceId = line.InvoiceId,
                CustomerPhone = line.CustomerPhoneSnapshot,
                CustomerEmail = NullIfWhiteSpace(line.CustomerEmailSnapshot),
                Amount = line.Amount, Currency = line.Currency, PaymentMethod = line.PaymentType,
                ChargeBasis = line.ChargeBasis, NegotiationReference = line.NegotiationReference,
                Notes = line.Notes, AddedByUserId = line.AddedByUserId, AddedBy = user.FullName
            };
        var count = await lineQuery.CountAsync(cancellationToken);
        if (includeLines)
            lines = await lineQuery.ToListAsync(cancellationToken);
        var response = MapHeader(manifest, count);
        var createdBy = await _context.UserAccounts.AsNoTracking().Where(x => x.Id == manifest.CreatedByUserId)
            .Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken);
        return response with { Lines = lines, CreatedBy = createdBy };
    }

    private async Task<ManifestLineResponse> MapLineAsync(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        return await (from line in _context.OperationalManifestEntries.AsNoTracking()
            join manifest in _context.OperationalManifests.AsNoTracking() on line.ManifestId equals manifest.Id
            join shipment in _context.CustomerShipments.AsNoTracking() on line.CustomerShipmentId equals shipment.Id
            join client in _context.Customers.AsNoTracking() on line.CustomerId equals client.Id
            join user in _context.UserAccounts.AsNoTracking() on line.AddedByUserId equals user.Id
            where line.Id == id && (branch.IsAllBranches || manifest.BranchId == branch.BranchId)
            select new ManifestLineResponse(line.Id, line.RowVersion, shipment.Id, line.InvoiceNumberSnapshot,
                client.Id, client.ClientNumber, line.CustomerNameSnapshot, line.SenderName, line.ReceiverName,
                line.PackageDescription, line.Contents, (int)line.Quantity, line.ActualWeightKg,
                line.CostWeightKg, line.HandoverMethod == ManifestHandoverMethod.HubCollection
                    ? ShipmentHandoverMethod.Pickup : ShipmentHandoverMethod.Delivery,
                line.HandoverCity ?? shipment.DestinationCityOrState, line.HandoverAddress,
                line.AddedAt, line.LastModifiedAt)
            {
                ManifestId = line.ManifestId, InvoiceId = line.InvoiceId,
                CustomerPhone = line.CustomerPhoneSnapshot,
                CustomerEmail = NullIfWhiteSpace(line.CustomerEmailSnapshot),
                Amount = line.Amount, Currency = line.Currency, PaymentMethod = line.PaymentType,
                ChargeBasis = line.ChargeBasis, NegotiationReference = line.NegotiationReference,
                Notes = line.Notes, AddedByUserId = line.AddedByUserId, AddedBy = user.FullName
            }).SingleAsync(cancellationToken);
    }

    private static ManifestResponse MapHeader(OperationalManifest x, int lineCount) => new(x.Id,
        x.ManifestNumber, x.RowVersion, x.BranchId, BranchKey(x.OriginCountryCode, x.OriginCity), x.ShipmentId,
        x.BookingReference, x.ManifestDate, PublicMethod(x.LogisticsMethod), x.OriginCountryCode,
        x.OriginCity, x.DestinationCountryCode, x.DestinationCity, x.CarrierName,
        x.CarrierReference, x.TrackingProvider, x.ContainerOrFlightNumber, lineCount,
        x.CreatedAt, x.LastModifiedAt, null)
    {
        ShipmentBookingId = x.ShipmentBookingId, ServiceRouteId = x.ServiceRouteId,
        RouteCode = x.RouteCode, RouteName = x.RouteName, CreatedByUserId = x.CreatedByUserId,
        BillOfLadingNumber = x.BillOfLadingNumber, FlightNumber = x.FlightNumber,
        VesselName = x.VesselName, VoyageNumber = x.VoyageNumber, Notes = x.Notes
    };

    private static void ValidateCreate(CreateManifestRequest request, ActiveBranchScope branch)
    {
        if (string.IsNullOrWhiteSpace(request.ShipmentName) || request.Destination is null ||
            string.IsNullOrWhiteSpace(request.Destination.CountryCode) ||
            string.IsNullOrWhiteSpace(request.Destination.City) || !Enum.IsDefined(request.Method))
            throw Rule("MANIFEST_DETAILS_REQUIRED", "Shipment name, Air/Sea method and destination are required.");
        if (CountryCode(request.Destination.CountryCode).Equals(CountryCode(branch.CountryCode),
                StringComparison.OrdinalIgnoreCase) &&
            request.Destination.City.Trim().Equals(branch.City, StringComparison.OrdinalIgnoreCase))
            throw Rule("DESTINATION_EQUALS_ORIGIN", "Destination cannot be the active origin hub.");
    }

    private static void EnsureRouteCompatible(OperationalManifest manifest, CustomerShipment shipment) =>
        EnsureRouteCompatible(manifest, shipment.OriginCountryCode, shipment.OriginCity,
            shipment.DestinationCountryCode, shipment.DestinationCityOrState);

    private static void EnsureRouteCompatible(OperationalManifest manifest, string originCountry,
        string originCity, string destinationCountry, string destinationCity)
    {
        var originMatches = CountryCode(manifest.OriginCountryCode) == CountryCode(originCountry) &&
            ComparableCity(manifest.OriginCity) == ComparableCity(originCity);
        var destinationMatches = CountryCode(manifest.DestinationCountryCode) ==
            CountryCode(destinationCountry) && CitiesCompatible(manifest.DestinationCity, destinationCity);
        if (!originMatches || !destinationMatches)
            throw Rule("MANIFEST_ROUTE_MISMATCH",
                $"This shipment route ({originCity} → {destinationCity}) does not match the manifest route " +
                $"({manifest.OriginCity} → {manifest.DestinationCity}).");
    }

    private static bool CitiesCompatible(string manifestCity, string shipmentCity)
    {
        var route = ComparableCity(manifestCity);
        var shipment = ComparableCity(shipmentCity);
        if (route == shipment) return true;
        var generic = manifestCity.ToLowerInvariant();
        return generic.Contains("receiver city") || generic.Contains("other ") ||
            generic.Contains("world") || generic.Contains("state") ||
            generic.Contains("region");
    }

    private static string ComparableCity(string value)
    {
        var normalized = value.Trim().ToUpperInvariant()
            .Replace("MAINLAND", "", StringComparison.Ordinal)
            .Replace("ISLAND", "", StringComparison.Ordinal)
            .Replace("WAREHOUSE", "", StringComparison.Ordinal)
            .Replace("COLLECTION", "", StringComparison.Ordinal)
            .Replace("CITY", "", StringComparison.Ordinal);
        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    private static void ValidateLineUpdate(UpdateManifestLineRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SenderName) || string.IsNullOrWhiteSpace(request.ReceiverName) ||
            string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.DestinationCity) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Pieces <= 0 || request.ActualWeightKg <= 0 ||
            request.ChargeableWeightKg < request.ActualWeightKg)
            throw Rule("INVALID_MANIFEST_LINE", "Sender, receiver, description, pieces, weights, destination and edit reason are required.");
        if (request.HandoverMethod == ShipmentHandoverMethod.Delivery &&
            string.IsNullOrWhiteSpace(request.DeliveryAddress))
            throw Rule("DELIVERY_ADDRESS_REQUIRED", "Delivery requires an address.");
    }

    private Guid RequireIdempotencyKey() => Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key)
        ? key : throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency key required",
            "Send a UUID Idempotency-Key header.");
    private long RequireVersion()
    {
        var raw = Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        return long.TryParse(raw, out var version) ? version : throw new ApiProblemException(428,
            "IF_MATCH_REQUIRED", "Version required", "Send the current row version in If-Match.");
    }
    private static void CheckVersion(long current, long supplied)
    {
        if (current != supplied) throw new ApiProblemException(412, "VERSION_MISMATCH", "The record changed",
            "Reload the current version before saving again.");
    }
    private static ManifestLogisticsMethod InternalMethod(CustomerShipmentMethod method) => method switch
    { CustomerShipmentMethod.Air => ManifestLogisticsMethod.Air, _ => ManifestLogisticsMethod.Ship };
    private static CustomerShipmentMethod PublicMethod(ManifestLogisticsMethod method) => method switch
    { ManifestLogisticsMethod.Air => CustomerShipmentMethod.Air, _ => CustomerShipmentMethod.Sea };
    private static ShipmentChargeBasis ChargeBasis(CustomerShipmentPricingMethod method) => method switch
    { CustomerShipmentPricingMethod.Weight => ShipmentChargeBasis.AirWeightAndDimensions,
      CustomerShipmentPricingMethod.BoxSizesAndItems => ShipmentChargeBasis.SeaBoxesAndItems,
      _ => ShipmentChargeBasis.Negotiated };
    private static DateTime UtcDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    private static string NormalizeName(string value) => string.Join(' ', value.Trim()
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string PersonName(string value) => CultureInfo.GetCultureInfo("en-GB").TextInfo
        .ToTitleCase(NormalizeName(value).ToLowerInvariant());
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string CountryCode(string? value)
    {
        var normalized = new string((value ?? string.Empty).Trim().ToUpperInvariant()
            .Where(char.IsLetter).ToArray());
        return normalized switch
        {
            "NIGERIA" or "NGA" => "NG",
            "UNITEDKINGDOM" or "GREATBRITAIN" or "BRITAIN" or "UK" or "GBR" => "GB",
            "CHINA" or "CHN" => "CN",
            "UNITEDSTATES" or "UNITEDSTATESOFAMERICA" or "USA" => "US",
            "CANADA" or "CAN" => "CA",
            _ when normalized.Length is 2 or 3 => normalized,
            _ => normalized[..Math.Min(5, normalized.Length)]
        };
    }
    private static string Quote(long value) => $"\"{value}\"";
    private static string RouteCode(string originCountry, string originCity, string destinationCountry,
        string destinationCity) => string.Join('-', originCountry.Trim().ToUpperInvariant(),
            Key(originCity), destinationCountry.Trim().ToUpperInvariant(), Key(destinationCity));
    private static string Key(string value) => new(value.Trim().ToUpperInvariant()
        .Where(char.IsLetterOrDigit).ToArray());
    private static string BranchKey(string countryCode, string city) =>
        countryCode.ToUpperInvariant() switch
        { "GB" => "manchester", "NG" => "lagos", "CN" => "guangzhou", _ => Key(city).ToLowerInvariant() };
    private static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);
    private async Task<DocumentBrandingData> DocumentBranding(Guid branchId,
        CancellationToken cancellationToken)
    {
        var company = await _context.CompanyProfiles.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.TradingName, x.RegisteredAddress })
            .FirstOrDefaultAsync(cancellationToken);
        var branch = await _context.Branches.AsNoTracking().Where(x => x.Id == branchId)
            .Select(x => new { x.Name, x.Code, x.Address }).SingleAsync(cancellationToken);
        return new DocumentBrandingData(company?.TradingName ?? "BiGSAM Global Logistics",
            $"{branch.Name} ({branch.Code})", branch.Address,
            company?.RegisteredAddress ?? string.Empty);
    }
    private static ApiProblemException Rule(string code, string detail) =>
        new(422, code, "Manifest rule was not satisfied", detail);
    private sealed record DocumentBrandingData(string CompanyName, string HubName,
        string HubAddress, string RegisteredAddress);
}
