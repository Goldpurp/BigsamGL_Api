using Domain.Enums;

namespace Api.Controllers;

public record CreateGuestRequest(string FullName, string Email, string Phone, string? CompanyName, string CountryCode,
    string Source, string ServiceInterest, string? Notes);
public record UpdateGuestRequest(GuestStatus Status, string? Notes, Guid? AssignedToUserId);
public record ConvertGuestRequest(Guid? AccountManagerUserId, Guid? PreferredBranchId, string BillingCurrency,
    int PaymentTermsDays, Guid? DuplicateReviewId = null, ClientAccountType? AccountType = null);
public record UpdateClientRequest(string FullName, string Phone, string? CompanyName, bool IsBusiness, string CountryCode,
    ClientStatus Status, Guid? AccountManagerUserId, Guid? PreferredBranchId, string TaxNumber,
    string BusinessRegistrationNumber, string BillingCurrency, decimal CreditLimit, int PaymentTermsDays,
    string? InternalNotes, string? RiskNotes, Guid? DuplicateReviewId = null,
    ClientAccountType? AccountType = null);
public record UpdateCustomerProfileRequest(string FullName, string Phone, string? CompanyName, bool IsBusiness,
    ClientAccountType? AccountType = null);
public record UpdateNotificationPreferencesRequest(bool EmailEnabled, bool SmsEnabled, bool WhatsAppEnabled,
    bool OperationalUpdates, bool MarketingUpdates);
public record CreateCustomerRequest(string FullName, string Email, decimal InitialDeposit, string CountryCode,
    string Phone = "", Guid? DuplicateReviewId = null, string? CompanyName = null,
    ClientAccountType? AccountType = null);
public record CheckClientMatchRequest(string FullName, string Email, string Phone, string? InvoiceNumber);
public record ResolveClientMatchRequest(ClientDuplicateDecision Decision, Guid? SelectedCustomerId, string Reason);
public record TopupRequest(decimal Amount, string? Reference);
public record CreateClientDirectoryRequest(string Name, string Phone, string? Email,
    Guid? DuplicateReviewId = null);
public record UpdateClientDirectoryRequest(string Name, string Phone, string? Email,
    Guid? DuplicateReviewId = null);
public record MergeClientRequest(Guid TargetClientId, string Reason);
