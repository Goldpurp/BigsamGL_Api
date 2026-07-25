using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class StaffLocationPing : BaseEntity
{
    public Guid? UserAccountId { get; set; }
    public Guid? StaffProfileId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal AccuracyMeters { get; set; }
    public DateTime CapturedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public StaffLocationSource Source { get; set; } = StaffLocationSource.BrowserGps;
    public bool ConsentConfirmed { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}
