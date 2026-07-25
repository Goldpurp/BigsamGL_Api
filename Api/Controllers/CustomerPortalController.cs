using System.Security.Claims;
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Features.CargoItems.Queries;
using Application.Mappings;
using Domain.Enums;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Roles = nameof(UserRole.Customer))]
[Route("api/customer-portal")]
public sealed class CustomerPortalController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    public CustomerPortalController(IApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<CustomerPortalDto>> Get(CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Customer profile not found.");
        var canonical = await (from shipment in _context.CustomerShipments.AsNoTracking()
                               join invoice in _context.Invoices.AsNoTracking()
                                   on shipment.InvoiceId equals invoice.Id
                               where shipment.CustomerId == customerId
                               orderby shipment.CreatedAt descending
                               select new { Shipment = shipment, Invoice = invoice })
            .Take(20).ToListAsync(cancellationToken);
        var legacy = await _context.CargoItems.AsNoTracking().Where(x =>
                x.CustomerId == customerId &&
                (!x.InvoiceId.HasValue || !_context.CustomerShipments.Any(shipment =>
                    shipment.InvoiceId == x.InvoiceId.Value)))
            .OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(cancellationToken);

        var active = await _context.CustomerShipments.CountAsync(x => x.CustomerId == customerId &&
            x.MovementState != CustomerShipmentMovementState.Delivered &&
            x.MovementState != CustomerShipmentMovementState.Cancelled, cancellationToken);
        var delivered = await _context.CustomerShipments.CountAsync(x => x.CustomerId == customerId &&
            x.MovementState == CustomerShipmentMovementState.Delivered, cancellationToken);
        active += await _context.CargoItems.CountAsync(x => x.CustomerId == customerId &&
            (!x.InvoiceId.HasValue || !_context.CustomerShipments.Any(shipment =>
                shipment.InvoiceId == x.InvoiceId.Value)) &&
            x.Status != CargoStatus.Delivered && x.Status != CargoStatus.Cancelled,
            cancellationToken);
        delivered += await _context.CargoItems.CountAsync(x => x.CustomerId == customerId &&
            (!x.InvoiceId.HasValue || !_context.CustomerShipments.Any(shipment =>
                shipment.InvoiceId == x.InvoiceId.Value)) &&
            x.Status == CargoStatus.Delivered, cancellationToken);

        var recent = canonical.Select(x =>
        {
            var shipment = x.Shipment;
            var invoice = x.Invoice;
            return new CustomerShipmentDto(shipment.Id, shipment.InvoiceNumber,
                shipment.InvoiceNumericFragment, shipment.Description,
                PortalStatus(shipment.MovementState),
                shipment.ChargeableWeightKg ?? shipment.ActualWeightKg ?? 0,
                Math.Max(0, invoice.TotalAmount - invoice.AmountPaid - invoice.AppliedCreditAmount),
                invoice.Currency, shipment.CreatedAt)
            {
                InvoiceNumber = shipment.InvoiceNumber,
                Method = shipment.Method.ToString(),
                Origin = shipment.OriginCity,
                Destination = string.Concat(shipment.DestinationCityOrState, ", ",
                    shipment.DestinationCountryCode),
                HandoverMethod = shipment.HandoverMethod.ToString(),
                PieceCount = shipment.PieceCount
            };
        }).Concat(legacy.Select(x => new CustomerShipmentDto(x.Id, x.TrackingNumber,
            x.MarkerCode, x.Description, x.Status, x.BillableWeightKg, x.ShippingCostAmount,
            x.ShippingCostCurrency, x.CreatedAt)
        {
            Method = x.AssignedTransportMode?.ToString()
        })).OrderByDescending(x => x.CreatedAt).Take(20).ToList();

        return Ok(new CustomerPortalDto(
            customer.ToDto(),
            active,
            delivered,
            recent));
    }

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateCustomerProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) return BadRequest("Full name is required.");
        var customerId = GetCustomerId();
        var customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Customer profile not found.");
        var userId = CurrentUserId;
        var user = await _context.UserAccounts.FirstAsync(x => x.Id == userId, cancellationToken);
        customer.FullName = request.FullName.Trim();
        customer.Phone = request.Phone.Trim();
        customer.CompanyName = request.CompanyName?.Trim();
        customer.AccountType = request.AccountType ?? (request.IsBusiness
            ? customer.AccountType == ClientAccountType.PartnerAgency
                ? ClientAccountType.PartnerAgency : ClientAccountType.Business
            : ClientAccountType.Individual);
        customer.IsBusiness = customer.AccountType != ClientAccountType.Individual;
        user.FullName = request.FullName.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("shipments/{cargoId:guid}/tracking")]
    public async Task<ActionResult> ShipmentTracking(Guid cargoId, CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        var cargo = await _context.CargoItems.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == cargoId && x.CustomerId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Shipment not found.");
        var tracking = await Mediator.Send(new GetPublicTrackingQuery(cargo.TrackingNumber), cancellationToken);
        return Ok(new
        {
            Client = await _context.Customers.AsNoTracking().Where(x => x.Id == customerId)
                .Select(x => new { Name = x.FullName, Number = x.Phone, x.Email }).FirstAsync(cancellationToken),
            Tracking = tracking
        });
    }

    [HttpGet("notification-preferences")]
    public async Task<ActionResult<NotificationPreference>> GetNotificationPreferences(CancellationToken cancellationToken)
    {
        var preference = await _context.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == CurrentCustomerId, cancellationToken)
            ?? new NotificationPreference { CustomerId = CurrentCustomerId };
        return Ok(preference);
    }

    [HttpPut("notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences(UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        var preference = await _context.NotificationPreferences.FirstOrDefaultAsync(x => x.CustomerId == CurrentCustomerId, cancellationToken);
        if (preference is null) { preference = new NotificationPreference { CustomerId = CurrentCustomerId }; _context.NotificationPreferences.Add(preference); }
        preference.EmailEnabled = request.EmailEnabled; preference.SmsEnabled = request.SmsEnabled;
        preference.WhatsAppEnabled = request.WhatsAppEnabled; preference.OperationalUpdates = request.OperationalUpdates;
        preference.MarketingUpdates = request.MarketingUpdates; preference.ConsentUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken); return NoContent();
    }

    private Guid GetCustomerId()
    {
        var value = User.FindFirstValue("customer_id");
        return Guid.TryParse(value, out var customerId)
            ? customerId
            : throw new UnauthorizedAccessException("Customer profile claim is missing.");
    }

    private static CargoStatus PortalStatus(CustomerShipmentMovementState state) => state switch
    {
        CustomerShipmentMovementState.New => CargoStatus.Logged,
        CustomerShipmentMovementState.ReadyForManifest => CargoStatus.ReceivedAtWarehouse,
        CustomerShipmentMovementState.AssignedToManifest => CargoStatus.AssignedToBatch,
        CustomerShipmentMovementState.OnTheWay => CargoStatus.InTransit,
        CustomerShipmentMovementState.Delivered => CargoStatus.Delivered,
        CustomerShipmentMovementState.Cancelled => CargoStatus.Cancelled,
        _ => CargoStatus.Logged
    };
}
