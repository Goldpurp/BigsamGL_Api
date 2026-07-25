using Domain.Enums;

namespace Application.Common.Interfaces;

public record PaymentSession(string Reference, string CheckoutUrl, string Status, DateTime ExpiresAt);
public record CarrierUpdate(string Provider, string ExternalReference, string ExternalEventId, string RawStatus,
    CargoStatus MappedStatus, string Title, string Description, string Location, DateTime OccurredAt,
    DateTime? EstimatedDeliveryAt);
public record StoredDocument(string StorageKey, string Checksum, string DownloadUrl);

public interface IIntegrationGateway
{
    Task SendNotificationAsync(IntegrationKind channel, string recipient, string template,
        object payload, CancellationToken cancellationToken);
    Task<PaymentSession> CreatePaymentSessionAsync(string reference, decimal amount, string currency,
        CancellationToken cancellationToken);
    Task<CarrierUpdate> GetCarrierUpdateAsync(string provider, string trackingNumber, CancellationToken cancellationToken);
    Task<StoredDocument> StoreDocumentAsync(string fileName, string contentType, long sizeBytes,
        CancellationToken cancellationToken);
}
