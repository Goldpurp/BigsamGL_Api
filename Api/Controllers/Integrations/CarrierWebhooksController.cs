using System.Security.Cryptography;
using System.Text;
using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Integrations;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("ProviderWebhook")]
[RequestTimeout("ProviderWebhook")]
[Route("api/integrations/carriers/{provider}/webhook")]
public sealed class CarrierWebhooksController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IShipmentTrackingService _tracking;
    private readonly IConfiguration _configuration;

    public CarrierWebhooksController(IApplicationDbContext context, IShipmentTrackingService tracking,
        IConfiguration configuration)
    {
        _context = context; _tracking = tracking; _configuration = configuration;
    }

    [HttpPost]
    public async Task<ActionResult> Receive(string provider, CarrierWebhookRequest request,
        [FromHeader(Name = "X-Carrier-Webhook-Secret")] string? suppliedSecret,
        CancellationToken cancellationToken)
    {
        var expectedSecret = _configuration["CarrierWebhooks:SharedSecret"];
        if (string.IsNullOrWhiteSpace(expectedSecret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Carrier webhook secret is not configured.");
        if (!SecretsMatch(expectedSecret, suppliedSecret)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.ExternalEventId) || string.IsNullOrWhiteSpace(request.ExternalTrackingNumber))
            return BadRequest("External event ID and tracking number are required.");

        var normalizedProvider = provider.Trim().ToUpperInvariant();
        var cargo = await _context.CargoItems.FirstOrDefaultAsync(x =>
            x.CarrierProvider == normalizedProvider && x.ExternalTrackingNumber == request.ExternalTrackingNumber,
            cancellationToken);
        if (cargo is null) return NotFound("No cargo is bound to this carrier tracking number.");

        var (status, eventType) = MapStatus(request.Status, request.IsException);
        var milestone = await _tracking.RecordAsync(cargo.Id, new ShipmentTrackingInput(status, eventType,
            request.Title, request.Description, request.Location, request.OccurredAt,
            MilestoneVisibility.CustomerVisible, request.EstimatedDeliveryAt, true,
            request.IsException, request.IssueCode, $"{normalizedProvider} Webhook", normalizedProvider,
            request.ExternalEventId), cancellationToken);
        return Ok(new { MilestoneId = milestone.Id, CargoId = cargo.Id, cargo.TrackingNumber, DuplicateSafe = true });
    }

    private static (CargoStatus Status, ShipmentEventType EventType) MapStatus(string value, bool isException)
    {
        if (isException) return (CargoStatus.OnHold, ShipmentEventType.Issue);
        return value.Trim().Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal)
            .ToLowerInvariant() switch
        {
            "pickupscheduled" or "awaitingpickup" => (CargoStatus.PickupScheduled, ShipmentEventType.PickupScheduled),
            "pickedup" or "collected" => (CargoStatus.PickedUp, ShipmentEventType.PickedUp),
            "incoming" or "inbound" => (CargoStatus.IncomingToWarehouse, ShipmentEventType.InTransit),
            "received" or "warehouse" => (CargoStatus.ReceivedAtWarehouse, ShipmentEventType.WarehouseReceived),
            "departed" => (CargoStatus.InTransit, ShipmentEventType.DepartedOrigin),
            "intransit" or "moving" => (CargoStatus.InTransit, ShipmentEventType.InTransit),
            "arrived" or "arrivedathub" => (CargoStatus.ArrivedAtDestinationHub, ShipmentEventType.ArrivedAtHub),
            "customs" => (CargoStatus.ArrivedAtDestinationHub, ShipmentEventType.Customs),
            "outfordelivery" => (CargoStatus.OutForDelivery, ShipmentEventType.OutForDelivery),
            "delivered" => (CargoStatus.Delivered, ShipmentEventType.Delivered),
            "delayed" => (CargoStatus.Delayed, ShipmentEventType.Delayed),
            "issue" or "exception" or "hold" => (CargoStatus.OnHold, ShipmentEventType.Issue),
            _ => (CargoStatus.InTransit, ShipmentEventType.CarrierUpdate)
        };
    }

    private static bool SecretsMatch(string expected, string? supplied)
    {
        if (string.IsNullOrEmpty(supplied)) return false;
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
