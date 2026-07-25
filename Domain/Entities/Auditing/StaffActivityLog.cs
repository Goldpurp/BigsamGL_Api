using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class StaffActivityLog : BaseEntity
{
    public Guid? UserAccountId { get; set; }
    public Guid? StaffProfileId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "HttpRequest";
    public string HttpMethod { get; set; } = string.Empty;
    public string RouteTemplate { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public string Controller { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public StaffActivityOutcome Outcome { get; set; }
    public long DurationMilliseconds { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public Guid? LocationPingId { get; set; }
    public StaffLocationSource LocationSource { get; set; }
    public StaffLocationFreshness LocationFreshness { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public DateTime? LocationCapturedAt { get; set; }
    public DateTime OccurredAt { get; set; }
}
