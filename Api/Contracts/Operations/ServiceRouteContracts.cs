using Domain.Enums;

namespace Api.Controllers.ServiceRoutes;

public sealed record SaveServiceRouteRequest(string ServiceCode, Guid OriginHubId,
    string DestinationCountryCode, string DestinationCity, string? DestinationRegion,
    CustomerShipmentMethod Method, string DisplayName, string EstimatedTimeline,
    string Reason);

public sealed record ArchiveServiceRouteRequest(string Reason);

public sealed record ServiceRouteResponse(Guid Id, long RowVersion, string ServiceCode,
    Guid OriginHubId, string OriginHubCode, string OriginHubName, string OriginCity,
    string OriginCountryCode, string DestinationCountryCode, string DestinationCity,
    string? DestinationRegion, CustomerShipmentMethod Method, string DisplayName,
    string EstimatedTimeline, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
