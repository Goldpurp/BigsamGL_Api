using Domain.Common;

namespace Domain.Entities;

public sealed class StaffNotification : BaseEntity
{
    public Guid UserAccountId { get; set; }
    public Guid? BranchId { get; set; }
    public string Category { get; set; } = "System";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Path { get; set; }
    public bool IsUrgent { get; set; }
    public DateTime? ReadAt { get; set; }
}

public sealed class UserNotificationPreference : BaseEntity
{
    public Guid UserAccountId { get; set; }
    public bool InAppEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool ShipmentAlerts { get; set; } = true;
    public bool FinanceAlerts { get; set; } = true;
    public bool SecurityAlerts { get; set; } = true;
}
