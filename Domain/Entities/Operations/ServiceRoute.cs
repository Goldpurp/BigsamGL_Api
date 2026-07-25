using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class ServiceRoute : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid OriginBranchId { get; set; }
    public Guid? DestinationBranchId { get; set; }
    public string DestinationCountryCode { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public string? DestinationRegion { get; set; }
    public string EstimatedTimeline { get; set; } = string.Empty;
    public TransportMode Mode { get; set; }
    public int TransitDaysMin { get; set; }
    public int TransitDaysMax { get; set; }
    public DayOfWeek? WeeklyDepartureDay { get; set; }
    public decimal VolumetricDivisor { get; set; } = 5000;
    public bool IsActive { get; set; } = true;
    public long RowVersion { get; set; } = 1;
}
