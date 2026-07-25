namespace Domain.Enums;

public enum LeadStage { New = 0, Contacted = 1, Qualified = 2, Quoted = 3, Won = 4, Lost = 5 }
public enum QuoteStatus { Draft = 0, Sent = 1, Accepted = 2, Rejected = 3, Expired = 4, Converted = 5 }
public enum GuestStatus { New = 0, Contacted = 1, Converted = 2, Archived = 3 }
public enum ClientStatus { Prospect = 0, Active = 1, OnHold = 2, Suspended = 3, Closed = 4 }
public enum LeadPriority { Low = 0, Normal = 1, High = 2, Urgent = 3 }
