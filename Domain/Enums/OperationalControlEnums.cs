namespace Domain.Enums;

public enum PickupState { Booked = 0, DriverAssigned = 1, Collected = 2, Cancelled = 3 }
public enum PickupPaymentState { Paid = 0, PartPaid = 1, Unpaid = 2 }

public enum ManifestTrackingStatus
{
    NotConnected = 0, Connected = 1, InTransit = 2, Delayed = 3, Arrived = 4, Delivered = 5
}

public enum TrackingEventSource { Provider = 0, Staff = 1 }
public enum TrackingEventReviewState { Draft = 0, Reviewed = 1, Published = 2, Rejected = 3 }
public enum CarrierTrackingReferenceType
{
    ContainerNumber = 0,
    BillOfLading = 1,
    BookingNumber = 2
}

public enum CargoItemIssueType
{
    ClientItemMismatch = 0, PhysicalMisplacement = 1, MissingPieces = 2,
    ExtraPieces = 3, Damage = 4, WrongLabel = 5, Other = 6
}

public enum CargoItemIssueSeverity { Low = 0, Medium = 1, High = 2, Critical = 3 }
public enum CargoItemIssueState { Open = 0, OnHold = 1, Released = 2, Resolved = 3 }
