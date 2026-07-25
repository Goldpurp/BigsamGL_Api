namespace Domain.Enums;

public enum EmailBroadcastPurpose
{
    General = 0,
    Operational = 1,
    Marketing = 2
}

public enum EmailBroadcastStatus
{
    Draft = 0,
    Scheduled = 1,
    Queued = 2,
    Sending = 3,
    Sent = 4,
    PartiallyFailed = 5,
    Cancelled = 6
}

public enum EmailBroadcastRecipientStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Skipped = 4
}

public enum EmailRecipientSourceType
{
    Client = 0,
    Contact = 1,
    Supplier = 2,
    Staff = 3
}
