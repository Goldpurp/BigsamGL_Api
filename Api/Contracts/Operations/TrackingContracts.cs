using Domain.Enums;

namespace Api.Controllers.Operations;

public sealed record TrackingConnectionRequest(string Provider, string ShippingCompanyName,
    string ExternalReference, string Reason,
    CarrierTrackingReferenceType ReferenceType = CarrierTrackingReferenceType.ContainerNumber);
public sealed record ManualTrackingEventRequest(string Title, string Description, string? Place,
    DateTime OccurredAt, DateTime? EstimatedAt, string Reason);
public sealed record ReviewTrackingEventRequest(bool Approve, string PublicTitle,
    string PublicDescription, string? Place, DateTime? EstimatedAt, string Reason);
public sealed record PublishTrackingEventRequest(string Confirmation, string Reason);
public sealed record NotifyTrackingClientsRequest(string Confirmation, string Subject, string Body);
public sealed record MscTrackingLookupRequest(Guid ManifestId,
    CarrierTrackingReferenceType TrackingType, string TrackingNumber);
public sealed record MscTrackingSyncRequest(Guid ManifestId,
    CarrierTrackingReferenceType TrackingType, string TrackingNumber, string Reason);
public sealed record ClientTrackingUpdateRequest(string CustomerVisibleStatus,
    string CustomerVisibleMessage, bool ShowToCustomer, string? Place,
    DateTime OccurredAt, DateTime? EstimatedAt, string Reason);
public sealed record TrackingWebhookRequest(string ExternalReference, string ProviderEventId,
    string Status, string Title, string Description, string? Place, DateTime OccurredAt,
    DateTime? EstimatedAt, bool IsException);

public sealed record TrackingEventResponse(Guid Id, long RowVersion, TrackingEventSource Source,
    TrackingEventReviewState ReviewState, string InternalTitle, string InternalDescription,
    string? ProviderStatus, string? PublicTitle, string? PublicDescription, string? Place,
    DateTime OccurredAt, DateTime? EstimatedAt, DateTime? ReviewedAt, DateTime? PublishedAt);
public sealed record TrackingConnectionResponse(Guid Id, long RowVersion, Guid ManifestId,
    string ManifestNumber, string ShipmentName, Guid BranchId, string Branch, string Provider,
    string ShippingCompanyName, string ExternalReference, CarrierTrackingReferenceType ReferenceType,
    ManifestTrackingStatus Status,
    string? ProviderStatus, string? LastKnownPlace, DateTime? EstimatedAt,
    DateTime? LastSuccessfulSyncAt, DateTime? NextSyncAt, string? PublicTrackingToken,
    DateTime CreatedAt, DateTime? LastModifiedAt);
public sealed record CarrierTrackingSyncResponse(Guid ManifestId, string ManifestNumber,
    string Provider, string ExternalReference, CarrierTrackingReferenceType ReferenceType,
    int EventsReceived, int NewEventsStored, int DuplicatesIgnored, bool IsMock,
    bool Replayed, DateTime SyncedAt, IReadOnlyList<TrackingEventResponse> Events);

public sealed record PublicManifestTrackingEventResponse(string Title, string Description,
    string? Place, DateTime OccurredAt, DateTime? EstimatedAt);

public sealed record PublicManifestTrackingResponse(string TrackingNumber, string ShipmentName,
    string Method, string Origin, string Destination, string ServiceName, string Status,
    string? LastKnownPlace, DateTime? EstimatedAt,
    IReadOnlyList<PublicManifestTrackingEventResponse> Events);
