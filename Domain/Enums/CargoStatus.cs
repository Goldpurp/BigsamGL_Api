namespace Domain.Enums;

public enum CargoStatus
{
    Logged = 0,
    ReceivedAtWarehouse = 1,
    AssignedToBatch = 2,
    InTransit = 3,
    ArrivedAtDestinationHub = 4,
    OutForDelivery = 5,
    Delivered = 6,
    Cancelled = 7,
    PickupScheduled = 8,
    PickedUp = 9,
    IncomingToWarehouse = 10,
    Delayed = 11,
    OnHold = 12
}
