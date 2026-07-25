using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class AppointmentAttendee : BaseEntity
{
    public Guid AppointmentId { get; set; }
    public AppointmentAttendeeType Type { get; set; }
    public Guid? UserAccountId { get; set; }
    public Guid? ContactId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public AppointmentResponseStatus ResponseStatus { get; set; } = AppointmentResponseStatus.Pending;
    public DateTime? RespondedAt { get; set; }
}
