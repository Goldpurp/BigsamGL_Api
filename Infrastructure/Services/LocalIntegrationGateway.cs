using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public sealed class LocalIntegrationGateway : IIntegrationGateway
{
    private readonly IApplicationDbContext _context;
    public LocalIntegrationGateway(
        IApplicationDbContext context,
        IConfiguration? configuration = null)
    {
        _context = context;
        var mode = configuration?["Integrations:Mode"];
        if (configuration is not null &&
            !string.Equals(mode, "Local", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The local integration provider is disabled outside the Local environment. Register reviewed live provider adapters before production startup.");
        }
    }

    public Task SendNotificationAsync(IntegrationKind channel, string recipient, string template,
        object payload, CancellationToken cancellationToken)
    {
        // Delivery content can include large attachments and sensitive message bodies. Keep the
        // provider audit record useful without duplicating that content in IntegrationMessages.
        AddMessage(channel, "SendNotification", new
            {
                recipient,
                template,
                payloadRecorded = false,
                note = "Delivery content is retained by the owning communication record."
            },
            new { accepted = true, providerMessageId = $"local-msg-{Guid.NewGuid():N}" });
        return Task.CompletedTask;
    }

    public Task<PaymentSession> CreatePaymentSessionAsync(string reference, decimal amount, string currency,
        CancellationToken cancellationToken)
    {
        var session = new PaymentSession(reference,
            $"https://local-payments.bigsamgl.test/checkout/{Uri.EscapeDataString(reference)}",
            "AwaitingPayment", DateTime.UtcNow.AddMinutes(30));
        AddMessage(IntegrationKind.Payment, "CreateCheckout", new { reference, amount, currency }, session);
        return Task.FromResult(session);
    }

    public Task<CarrierUpdate> GetCarrierUpdateAsync(string provider, string trackingNumber, CancellationToken cancellationToken)
    {
        var locations = new[] { "Manchester Warehouse", "London Heathrow Cargo Hub", "In transit", "Lagos Cargo Terminal" };
        var index = Math.Abs(trackingNumber.GetHashCode(StringComparison.Ordinal)) % locations.Length;
        var status = index < 2 ? "Processing" : "InTransit";
        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "GDH" : provider.Trim().ToUpperInvariant();
        var update = new CarrierUpdate(normalizedProvider, trackingNumber,
            $"local-{normalizedProvider}-{trackingNumber}-{DateTime.UtcNow:yyyyMMddHH}", status,
            status == "InTransit" ? CargoStatus.InTransit : CargoStatus.IncomingToWarehouse,
            status == "InTransit" ? "Carrier transit update" : "Carrier processing update",
            $"{normalizedProvider} movement update recorded by the local operations adapter.",
            locations[index], DateTime.UtcNow, DateTime.UtcNow.AddDays(index + 2));
        AddMessage(IntegrationKind.CarrierTracking, "GetTrackingUpdate", new { provider = normalizedProvider, trackingNumber }, update);
        return Task.FromResult(update);
    }

    public Task<StoredDocument> StoreDocumentAsync(string fileName, string contentType, long sizeBytes,
        CancellationToken cancellationToken)
    {
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{fileName}:{contentType}:{sizeBytes}")));
        var key = $"local/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}/{fileName}";
        var stored = new StoredDocument(key, checksum, $"https://local-files.bigsamgl.test/{key}");
        AddMessage(IntegrationKind.DocumentStorage, "StoreDocumentMetadata", new { fileName, contentType, sizeBytes }, stored);
        return Task.FromResult(stored);
    }

    private void AddMessage(IntegrationKind kind, string operation, object request, object response) =>
        _context.IntegrationMessages.Add(new IntegrationMessage
        {
            Kind = kind,
            Provider = "BiGSAM Local Provider",
            Operation = operation,
            CorrelationId = $"local-{Guid.NewGuid():N}",
            RequestJson = JsonSerializer.Serialize(request),
            ResponseJson = JsonSerializer.Serialize(response),
            Status = IntegrationMessageStatus.Succeeded,
            ProcessedAt = DateTime.UtcNow
        });
}
