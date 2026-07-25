using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class OmniChannelNotificationService : IOmniChannelNotificationService
{
    private readonly ILogger<OmniChannelNotificationService> _logger;

    public OmniChannelNotificationService(ILogger<OmniChannelNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendCargoStatusUpdateAsync(Customer customer, CargoItem cargoItem, string? messageOverride = null, CancellationToken cancellationToken = default)
    {
        var subject = $"[BiGSAM Global Logistics] Cargo tracking update - {cargoItem.TrackingNumber}";
        var body = messageOverride ?? $"Hello {customer.FullName}, your cargo item ({cargoItem.Description}) has been updated. Status: {cargoItem.Status}. Marker: {cargoItem.MarkerCode}.";

        _logger.LogInformation(@"
============================================================
[OUTGOING OMNI-CHANNEL NOTIFICATION: CARGO STATUS UPDATE]
============================================================
To: {Email}
Subject: {Subject}

--- EMAIL (Amazon SES) ---
{Body}

--- SMS (Termii) ---
BiGSAM update: Item {Tracking} status is now '{Status}'. Marker: {Marker}.

--- WHATSAPP (Twilio) ---
*BiGSAM Global Logistics Limited*
Hi {Name}, your package {Marker} ({Description}) status is updated to: *{Status}*.
Tracking reference: {Tracking}
============================================================", 
            customer.Email, subject, body, cargoItem.TrackingNumber, cargoItem.Status, cargoItem.MarkerCode, 
            customer.FullName, cargoItem.MarkerCode, cargoItem.Description, cargoItem.Status, cargoItem.TrackingNumber);

        return Task.CompletedTask;
    }

    public Task SendLowBalanceAlertAsync(Customer customer, decimal requiredAmount, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(@"
============================================================
[OUTGOING OMNI-CHANNEL NOTIFICATION: LOW WALLET BALANCE]
============================================================
To: {Email}
Subject: [ACTION REQUIRED] Upfront Logistics Charge Payment Notice

--- EMAIL (Amazon SES) ---
Dear {Name},

We processed your new cargo item. Your wallet has been auto-debited.
However, there is an outstanding logistics balance of {Currency} {Amount:F2}.
Please fund your BiGSAM wallet to avoid customs routing delays.

Best regards,
Billing Operations
BiGSAM Global Logistics Limited

--- SMS (Termii) ---
BiGSAM alert: Action required. Your recent cargo has an outstanding balance of {Currency} {Amount:F2}. Please add funds.
============================================================", 
            customer.Email, customer.FullName, customer.WalletCurrency, requiredAmount, customer.WalletCurrency, requiredAmount);

        return Task.CompletedTask;
    }

    public Task SendBatchDispatchAlertAsync(List<Customer> customers, ShippingBatch batch, CancellationToken cancellationToken = default)
    {
        foreach (var customer in customers)
        {
            _logger.LogInformation(@"
============================================================
[OUTGOING OMNI-CHANNEL NOTIFICATION: SHIPPING BATCH DISPATCH]
============================================================
To: {Email}
Subject: [BiGSAM Global Logistics] Your consolidated shipment has been dispatched - {Code}

--- EMAIL (Amazon SES) ---
Hello {Name},

Your cargo in BiGSAM consolidation {Code} is now: {Status}.
BiGSAM dispatch reference: {Code}
Origin: {Origin} -> Destination: {Destination}
External carrier reference: {Bill}

--- SMS (Termii) ---
BiGSAM: Your consolidated cargo under {Code} is now {Status}. Route: {Origin} → {Destination}.
============================================================", 
                customer.Email, batch.BatchCode, customer.FullName, batch.BatchCode, batch.Status, batch.BatchCode,
                batch.OriginHub, batch.DestinationHub, batch.AirwayBillNumber ?? "Pending", batch.BatchCode, 
                batch.Status, batch.OriginHub, batch.DestinationHub);
        }

        return Task.CompletedTask;
    }
}
