using Domain.Common;

namespace Domain.Entities;

public class NotificationPreference : BaseEntity
{
    public Guid CustomerId { get; set; }
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = true;
    public bool WhatsAppEnabled { get; set; } = true;
    public bool OperationalUpdates { get; set; } = true;
    public bool MarketingUpdates { get; set; }
    public DateTime ConsentUpdatedAt { get; set; } = DateTime.UtcNow;
}
