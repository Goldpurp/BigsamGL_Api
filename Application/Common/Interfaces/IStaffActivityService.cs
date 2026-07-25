using Domain.Enums;

namespace Application.Common.Interfaces;

public record StaffLocationObservation(decimal Latitude, decimal Longitude, decimal AccuracyMeters,
    DateTime CapturedAt, StaffLocationSource Source, string DeviceId, Guid? LocationPingId = null);

public record StaffActivityInput(Guid UserAccountId, string ActivityType, string HttpMethod,
    string RouteTemplate, string Path, string QueryString, string Controller, string ActionName,
    int StatusCode, StaffActivityOutcome Outcome, long DurationMilliseconds, string IpAddress,
    string UserAgent, string DeviceId, string CorrelationId, string SessionId, DateTime OccurredAt,
    StaffLocationObservation? Location);

public interface IStaffActivityService
{
    Task RecordAsync(StaffActivityInput input, CancellationToken cancellationToken);
}
