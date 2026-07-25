using Domain.Enums;

namespace Api.Controllers;

public sealed record AppointmentAttendeeRequest(AppointmentAttendeeType Type,
    Guid? UserAccountId, Guid? ContactId, string Name, string Email, bool IsRequired);

public sealed record CreateAppointmentRequest(string Title, AppointmentType Type, DateTime StartsAt,
    DateTime EndsAt, string TimeZone, Guid? AssignedUserId, Guid? BranchId, Guid? ContactId,
    Guid? CustomerId, string Location, string? MeetingUrl,
    string Description, DateTime? ReminderAt, bool IsPrivate,
    IReadOnlyList<AppointmentAttendeeRequest> Attendees);

public sealed record ChangeAppointmentStatusRequest(AppointmentStatus Status, string? Reason);
public sealed record RespondToAppointmentRequest(AppointmentResponseStatus Response);
