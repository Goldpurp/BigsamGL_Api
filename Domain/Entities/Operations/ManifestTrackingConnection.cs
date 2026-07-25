using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class ManifestTrackingConnection : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid ManifestId { get; set; }
    public long RowVersion { get; set; } = 1;
    public string Provider { get; set; } = string.Empty;
    public string ShippingCompanyName { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public CarrierTrackingReferenceType ReferenceType { get; set; } =
        CarrierTrackingReferenceType.ContainerNumber;
    public string PublicTokenHash { get; set; } = string.Empty;
    public ManifestTrackingStatus Status { get; set; } = ManifestTrackingStatus.Connected;
    public string? ProviderStatus { get; set; }
    public string? LastKnownPlace { get; set; }
    public DateTime? EstimatedAt { get; set; }
    public DateTime? LastSuccessfulSyncAt { get; set; }
    public DateTime? NextSyncAt { get; set; }
}

public sealed class ManifestTrackingEvent : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid ConnectionId { get; set; }
    public long RowVersion { get; set; } = 1;
    public string? ProviderEventId { get; set; }
    public TrackingEventSource Source { get; set; }
    public TrackingEventReviewState ReviewState { get; set; } = TrackingEventReviewState.Draft;
    public string InternalTitle { get; set; } = string.Empty;
    public string InternalDescription { get; set; } = string.Empty;
    public string? ProviderStatus { get; set; }
    public string? RawPayloadProtected { get; set; }
    public string? PublicTitle { get; set; }
    public string? PublicDescription { get; set; }
    public string? Place { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? EstimatedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
}
