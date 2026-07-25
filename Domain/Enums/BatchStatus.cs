namespace Domain.Enums;

public enum BatchStatus
{
    Draft = 0,
    Locked = 1,
    Dispatched = 2,
    ArrivedAtCustoms = 3,
    Released = 4,
    Delivered = 5,
    Delayed = 6
}
