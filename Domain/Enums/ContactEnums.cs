namespace Domain.Enums;

public enum ContactType
{
    Client = 0,
    Supplier = 1,
    PartnerAgency = 2,
    Prospect = 3,
    Carrier = 4,
    GovernmentAgency = 5,
    Other = 6,
    Director = 7,
    Shareholder = 8,
    Staff = 9,
    ServiceProvider = 10
}

public enum ContactStatus { Active = 0, Inactive = 1, DoNotContact = 2, Archived = 3 }

public enum ContactInteractionType
{
    Note = 0,
    PhoneCall = 1,
    Email = 2,
    WhatsApp = 3,
    Meeting = 4,
    Visit = 5,
    Complaint = 6
}
