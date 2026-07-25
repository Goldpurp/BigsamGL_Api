namespace Domain.Enums;

public enum CompanyPurchaseCategory
{
    Maintenance = 0,
    ProfessionalService = 1,
    LogisticsService = 2,
    Supplies = 3,
    Equipment = 4,
    Utilities = 5,
    Rent = 6,
    Regulatory = 7,
    Other = 8
}

public enum CompanyPurchaseStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Rejected = 5,
    Cancelled = 6
}

public enum PurchasePayoutStatus { Confirmed = 0, Voided = 1 }

public enum PurchaseReceivingStatus
{
    NotRequired = 0,
    Pending = 1,
    PartiallyReceived = 2,
    Received = 3
}

/// <summary>Separates physical inputs from services and other money-out purchases.</summary>
public enum CompanyPurchaseType
{
    GoodsInput = 0,
    Service = 1,
    Asset = 2,
    Other = 3
}
