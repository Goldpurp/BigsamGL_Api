using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("api/appointments")]
public sealed class AppointmentsController : ApiControllerBase
{
    private static readonly AppointmentStatus[] BlockingStatuses =
    [AppointmentStatus.Tentative, AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.InProgress];

    private readonly IApplicationDbContext _context;
    private readonly ICompanyDocumentNumberGenerator _numbers;
    private readonly IIntegrationGateway _integrations;

    public AppointmentsController(IApplicationDbContext context, ICompanyDocumentNumberGenerator numbers,
        IIntegrationGateway integrations)
    {
        _context = context; _numbers = numbers; _integrations = integrations;
    }

    [HttpPost]
    public async Task<ActionResult> Create(CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(request, cancellationToken);
        if (error is not null) return BadRequest(error);
        var staffIds = request.Attendees.Where(x => x.UserAccountId.HasValue)
            .Select(x => x.UserAccountId!.Value).Append(CurrentUserId);
        if (request.AssignedUserId.HasValue) staffIds = staffIds.Append(request.AssignedUserId.Value);
        var conflicts = await FindConflictsAsync(staffIds.Distinct().ToArray(), request.StartsAt.ToUniversalTime(),
            request.EndsAt.ToUniversalTime(), null, cancellationToken);
        if (conflicts.Count > 0) return Conflict(new
        {
            Message = "One or more staff members already have an appointment during this time.", Conflicts = conflicts
        });

        var appointment = new Appointment
        {
            AppointmentNumber = await _numbers.NextAppointmentAsync(cancellationToken),
            OrganizerUserId = CurrentUserId
        };
        Apply(appointment, request);
        var attendees = BuildAttendees(appointment.Id, request.Attendees);
        _context.Appointments.Add(appointment);
        _context.AppointmentAttendees.AddRange(attendees);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = appointment.Id },
            new { Appointment = appointment, Attendees = attendees });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var appointment = await _context.Appointments.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Appointment not found.");
        if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            return Conflict("Completed or cancelled appointments cannot be edited.");
        var error = await ValidateAsync(request, cancellationToken);
        if (error is not null) return BadRequest(error);
        var staffIds = request.Attendees.Where(x => x.UserAccountId.HasValue)
            .Select(x => x.UserAccountId!.Value).Append(appointment.OrganizerUserId);
        if (request.AssignedUserId.HasValue) staffIds = staffIds.Append(request.AssignedUserId.Value);
        var conflicts = await FindConflictsAsync(staffIds.Distinct().ToArray(), request.StartsAt.ToUniversalTime(),
            request.EndsAt.ToUniversalTime(), id, cancellationToken);
        if (conflicts.Count > 0) return Conflict(new
        {
            Message = "One or more staff members already have an appointment during this time.", Conflicts = conflicts
        });
        Apply(appointment, request);
        appointment.ReminderSentAt = null;
        var oldAttendees = await _context.AppointmentAttendees.Where(x => x.AppointmentId == id)
            .ToListAsync(cancellationToken);
        _context.AppointmentAttendees.RemoveRange(oldAttendees);
        var attendees = BuildAttendees(id, request.Attendees);
        _context.AppointmentAttendees.AddRange(attendees);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { Appointment = appointment, Attendees = attendees });
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] AppointmentStatus? status, [FromQuery] AppointmentType? type,
        [FromQuery] Guid? branchId, [FromQuery] Guid? staffUserId, [FromQuery] Guid? contactId,
        [FromQuery] Guid? customerId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Appointments.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(x => x.EndsAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.StartsAt <= to.Value.ToUniversalTime());
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        if (contactId.HasValue) query = query.Where(x => x.ContactId == contactId);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (staffUserId.HasValue)
        {
            var staff = staffUserId.Value;
            var attendeeAppointmentIds = _context.AppointmentAttendees.AsNoTracking()
                .Where(x => x.UserAccountId == staff).Select(x => x.AppointmentId);
            query = query.Where(x => x.OrganizerUserId == staff || x.AssignedUserId == staff ||
                attendeeAppointmentIds.Contains(x.Id));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.AppointmentNumber.ToLower().Contains(value) ||
                x.Title.ToLower().Contains(value) || x.Location.ToLower().Contains(value) ||
                x.Description.ToLower().Contains(value));
        }
        return Ok(await query.OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await _context.Appointments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken);
        if (appointment is null) return NotFound();
        var attendees = await _context.AppointmentAttendees.AsNoTracking()
            .Where(x => x.AppointmentId == id).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Ok(new { Appointment = appointment, Attendees = attendees });
    }

    [HttpGet("availability")]
    public async Task<ActionResult> Availability([FromQuery] Guid staffUserId, [FromQuery] DateTime startsAt,
        [FromQuery] DateTime endsAt, [FromQuery] Guid? excludeAppointmentId,
        CancellationToken cancellationToken)
    {
        if (startsAt == default || endsAt <= startsAt) return BadRequest("A valid time range is required.");
        var conflicts = await FindConflictsAsync([staffUserId], startsAt.ToUniversalTime(),
            endsAt.ToUniversalTime(), excludeAppointmentId, cancellationToken);
        return Ok(new { Available = conflicts.Count == 0, Conflicts = conflicts });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult> ChangeStatus(Guid id, ChangeAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var appointment = await _context.Appointments.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Appointment not found.");
        if (!IsValidTransition(appointment.Status, request.Status))
            return Conflict($"Cannot change an appointment from {appointment.Status} to {request.Status}.");
        if (request.Status == AppointmentStatus.Cancelled && string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("A cancellation reason is required.");
        appointment.Status = request.Status;
        if (request.Status == AppointmentStatus.Completed) appointment.CompletedAt = DateTime.UtcNow;
        if (request.Status == AppointmentStatus.Cancelled)
        {
            appointment.CancelledAt = DateTime.UtcNow;
            appointment.CancellationReason = request.Reason!.Trim();
        }
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(appointment);
    }

    [HttpPatch("{id:guid}/attendees/{attendeeId:guid}/response")]
    public async Task<ActionResult> Respond(Guid id, Guid attendeeId, RespondToAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var attendee = await _context.AppointmentAttendees.FirstOrDefaultAsync(
            x => x.Id == attendeeId && x.AppointmentId == id, cancellationToken)
            ?? throw new KeyNotFoundException("Appointment attendee not found.");
        if (attendee.UserAccountId != CurrentUserId) return Forbid();
        attendee.ResponseStatus = request.Response;
        attendee.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(attendee);
    }

    [HttpPost("{id:guid}/reminder")]
    public async Task<ActionResult> SendReminder(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await _context.Appointments.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Appointment not found.");
        if (!BlockingStatuses.Contains(appointment.Status))
            return Conflict("Reminders can only be sent for active appointments.");
        var emails = await _context.AppointmentAttendees.AsNoTracking()
            .Where(x => x.AppointmentId == id && x.Email != "")
            .Select(x => x.Email).Distinct().ToListAsync(cancellationToken);
        foreach (var email in emails)
            await _integrations.SendNotificationAsync(IntegrationKind.Email, email,
                "AppointmentReminder", new { appointment.AppointmentNumber, appointment.Title,
                    appointment.StartsAt, appointment.EndsAt, appointment.TimeZone,
                    appointment.Location, appointment.MeetingUrl }, cancellationToken);
        appointment.ReminderSentAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { appointment.Id, appointment.ReminderSentAt, RecipientCount = emails.Count,
            Provider = "Configured provider" });
    }

    private async Task<string?> ValidateAsync(CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return "Appointment title is required.";
        var start = request.StartsAt.ToUniversalTime(); var end = request.EndsAt.ToUniversalTime();
        if (request.StartsAt == default || request.EndsAt == default || end <= start)
            return "Appointment end time must be after its start time.";
        if (end - start > TimeSpan.FromDays(1)) return "An appointment cannot exceed 24 hours.";
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone.Trim()); }
        catch (TimeZoneNotFoundException) { return "Time zone was not found."; }
        if (request.ReminderAt.HasValue && request.ReminderAt.Value.ToUniversalTime() >= start)
            return "Reminder time must be before the appointment starts.";
        if (request.Attendees.Count > 100) return "An appointment supports at most 100 attendees.";
        if (request.AssignedUserId.HasValue && !await ActiveStaffExistsAsync(request.AssignedUserId.Value, cancellationToken))
            return "Assigned active staff member was not found.";
        if (request.BranchId.HasValue && !await _context.Branches.AnyAsync(x => x.Id == request.BranchId && x.IsActive, cancellationToken))
            return "Active branch was not found.";
        if (request.ContactId.HasValue && !await _context.Contacts.AnyAsync(x => x.Id == request.ContactId, cancellationToken))
            return "Contact was not found.";
        if (request.CustomerId.HasValue && !await _context.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken))
            return "Customer was not found.";
        var userIds = request.Attendees.Where(x => x.Type == AppointmentAttendeeType.Staff)
            .Select(x => x.UserAccountId).ToArray();
        if (userIds.Any(x => !x.HasValue)) return "Staff attendees must reference a user account.";
        var distinctUsers = userIds.Select(x => x!.Value).Distinct().ToArray();
        if (distinctUsers.Length != userIds.Length) return "Staff attendees cannot be duplicated.";
        var activeCount = await _context.UserAccounts.CountAsync(x => distinctUsers.Contains(x.Id) &&
            x.Role != UserRole.Customer && x.Status == AccountStatus.Active, cancellationToken);
        if (activeCount != distinctUsers.Length) return "One or more active staff attendees were not found.";
        var contactIds = request.Attendees.Where(x => x.Type == AppointmentAttendeeType.Contact)
            .Select(x => x.ContactId).ToArray();
        if (contactIds.Any(x => !x.HasValue)) return "Contact attendees must reference a contact.";
        var distinctContacts = contactIds.Select(x => x!.Value).Distinct().ToArray();
        if (await _context.Contacts.CountAsync(x => distinctContacts.Contains(x.Id), cancellationToken) != distinctContacts.Length)
            return "One or more contact attendees were not found.";
        if (request.Attendees.Any(x => x.Type == AppointmentAttendeeType.External &&
            (string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Email))))
            return "External attendees require a name and email address.";
        return null;
    }

    private async Task<List<object>> FindConflictsAsync(Guid[] staffIds, DateTime startsAt, DateTime endsAt,
        Guid? excludedId, CancellationToken cancellationToken)
    {
        var attendeeIds = _context.AppointmentAttendees.AsNoTracking()
            .Where(x => x.UserAccountId.HasValue && staffIds.Contains(x.UserAccountId.Value))
            .Select(x => x.AppointmentId);
        var query = _context.Appointments.AsNoTracking().Where(x =>
            (!excludedId.HasValue || x.Id != excludedId.Value) && BlockingStatuses.Contains(x.Status) &&
            x.StartsAt < endsAt && x.EndsAt > startsAt &&
            (staffIds.Contains(x.OrganizerUserId) || (x.AssignedUserId.HasValue && staffIds.Contains(x.AssignedUserId.Value)) ||
             attendeeIds.Contains(x.Id)));
        return await query.OrderBy(x => x.StartsAt).Select(x => (object)new
        { x.Id, x.AppointmentNumber, x.Title, x.StartsAt, x.EndsAt }).ToListAsync(cancellationToken);
    }

    private async Task<bool> ActiveStaffExistsAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.UserAccounts.AnyAsync(x => x.Id == id && x.Role != UserRole.Customer &&
            x.Status == AccountStatus.Active, cancellationToken);

    private static List<AppointmentAttendee> BuildAttendees(Guid appointmentId,
        IEnumerable<AppointmentAttendeeRequest> requests) => requests.Select(x => new AppointmentAttendee
        {
            AppointmentId = appointmentId, Type = x.Type, UserAccountId = x.UserAccountId,
            ContactId = x.ContactId, Name = x.Name.Trim(), Email = x.Email.Trim().ToLowerInvariant(),
            IsRequired = x.IsRequired
        }).ToList();

    private static void Apply(Appointment value, CreateAppointmentRequest request)
    {
        value.Title = request.Title.Trim(); value.Type = request.Type;
        value.StartsAt = request.StartsAt.ToUniversalTime(); value.EndsAt = request.EndsAt.ToUniversalTime();
        value.TimeZone = request.TimeZone.Trim(); value.AssignedUserId = request.AssignedUserId;
        value.BranchId = request.BranchId; value.ContactId = request.ContactId;
        value.CustomerId = request.CustomerId;
        value.Location = request.Location.Trim();
        value.MeetingUrl = string.IsNullOrWhiteSpace(request.MeetingUrl) ? null : request.MeetingUrl.Trim();
        value.Description = request.Description.Trim();
        value.ReminderAt = request.ReminderAt?.ToUniversalTime(); value.IsPrivate = request.IsPrivate;
    }

    private static bool IsValidTransition(AppointmentStatus current, AppointmentStatus next) =>
        current == next || (current, next) switch
        {
            (AppointmentStatus.Tentative, AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.Cancelled) => true,
            (AppointmentStatus.Scheduled, AppointmentStatus.Confirmed or AppointmentStatus.InProgress or AppointmentStatus.Cancelled or AppointmentStatus.NoShow) => true,
            (AppointmentStatus.Confirmed, AppointmentStatus.InProgress or AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow) => true,
            (AppointmentStatus.InProgress, AppointmentStatus.Completed or AppointmentStatus.Cancelled) => true,
            _ => false
        };
}
