namespace Domain.Enums;

public enum AppointmentType
{
    ExecutiveMeeting = 0,
    ClientMeeting = 1,
    Pickup = 2,
    Delivery = 3,
    WarehouseInspection = 4,
    StaffMeeting = 5,
    SupplierMeeting = 6,
    CustomsAppointment = 7,
    Other = 8,
    Maintenance = 9,
    Training = 10,
    ComplianceDeadline = 11
}

public enum AppointmentStatus
{
    Tentative = 0,
    Scheduled = 1,
    Confirmed = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6
}

public enum AppointmentAttendeeType { Staff = 0, Contact = 1, External = 2 }
public enum AppointmentResponseStatus { Pending = 0, Accepted = 1, Declined = 2, Tentative = 3 }
