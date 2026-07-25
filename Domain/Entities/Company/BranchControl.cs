using Domain.Common;

namespace Domain.Entities;

public class BranchControl : BaseEntity
{
    public Guid BranchId { get; set; }
    public bool IsBookingEnabled { get; set; } = true;
    public bool IsReceivingEnabled { get; set; } = true;
    public bool IsDispatchEnabled { get; set; } = true;
    public decimal CapacityAlertPercent { get; set; } = 85;
    public int? DailyCargoLimit { get; set; }
    public string? EmergencyNotice { get; set; }
    public Guid? LastControlledByUserId { get; set; }
    public DateTime LastControlledAt { get; set; } = DateTime.UtcNow;
}
