using Domain.Enums;

namespace Application.Common.Interfaces;

public record MscTrackingEvent(string ExternalEventId, string EventClassifier, string EventTypeCode,
    string EventStatus, string Description, string Location, DateTime OccurredAt,
    DateTime? EstimatedArrivalAt, string RawPayloadJson);
public record MscContainerSnapshot(string ContainerNumber, string Status, string CurrentLocation,
    DateTime? EstimatedArrivalAt, IReadOnlyList<MscTrackingEvent> Events, bool IsMock);

public interface IMscTrackingGateway
{
    Task<MscContainerSnapshot> FetchAsync(string reference,
        CarrierTrackingReferenceType referenceType, CancellationToken cancellationToken);
    Task<MscContainerSnapshot> FetchContainerAsync(string containerNumber, string? bookingReference,
        string? billOfLadingNumber, CancellationToken cancellationToken);
}

public sealed class MscTrackingUnavailableException : Exception
{
    public MscTrackingUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

public sealed class MscTrackingReferenceNotFoundException : Exception
{
    public MscTrackingReferenceNotFoundException(string reference,
        CarrierTrackingReferenceType referenceType)
        : base($"MSC could not find {referenceType} reference '{reference}'.") { }
}
