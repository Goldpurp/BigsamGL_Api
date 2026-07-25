using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class Appointment : BaseEntity
{
    public string AppointmentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public AppointmentType Type { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string TimeZone { get; set; } = "Africa/Lagos";
    public Guid OrganizerUserId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesLeadId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? MeetingUrl { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? ReminderAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsPrivate { get; set; }
}
