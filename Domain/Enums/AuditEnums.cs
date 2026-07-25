namespace Domain.Enums;

public enum StaffActivityOutcome
{
    Succeeded = 0,
    ClientError = 1,
    Unauthorized = 2,
    Forbidden = 3,
    RateLimited = 4,
    ServerError = 5
}

public enum StaffLocationSource
{
    None = 0,
    BrowserGps = 1,
    RequestHeaders = 2,
    LastKnownBrowserGps = 3
}

public enum StaffLocationFreshness { Unavailable = 0, Current = 1, Stale = 2 }
