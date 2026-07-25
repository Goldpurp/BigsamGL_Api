namespace Domain.Enums;

public enum ClientDuplicateReviewStatus
{
    Pending = 0,
    ExistingClientSelected = 1,
    NewClientApproved = 2,
    Expired = 3,
    Cancelled = 4
}

public enum ClientDuplicateDecision
{
    UseExistingClient = 0,
    CreateNewClient = 1,
    Cancel = 2
}
