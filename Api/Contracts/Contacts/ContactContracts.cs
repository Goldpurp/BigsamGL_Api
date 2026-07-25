using Domain.Enums;

namespace Api.Controllers;

public sealed record CreateContactRequest(ContactType Type, string FullName, string? CompanyName,
    string? JobTitle, string Email, string Phone, string CountryCode, string? Address,
    string? Tags, string? Notes, Guid? CustomerId, Guid? SupplierId,
    Guid? PreferredBranchId, Guid? OwnerUserId, bool IsPrimary,
    bool IgnorePotentialDuplicate = false);

public sealed record UpdateContactRequest(ContactType Type, ContactStatus Status, string FullName,
    string? CompanyName, string? JobTitle, string Email, string Phone, string CountryCode,
    string? Address, string? Tags, string? Notes, Guid? CustomerId, Guid? SupplierId,
    Guid? PreferredBranchId, Guid? OwnerUserId, bool IsPrimary,
    bool IgnorePotentialDuplicate = false);

public sealed record AddContactInteractionRequest(ContactInteractionType Type, string Subject,
    string Details, DateTime OccurredAt);
