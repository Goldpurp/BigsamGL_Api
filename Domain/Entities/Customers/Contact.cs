using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class Contact : BaseEntity
{
    public ContactType Type { get; set; }
    public ContactStatus Status { get; set; } = ContactStatus.Active;
    public string FullName { get; set; } = string.Empty;
    public string NormalizedFullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? NormalizedPhone { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? SalesLeadId { get; set; }
    public Guid? PreferredBranchId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime? LastContactedAt { get; set; }
    public DateTime? NextFollowUpAt { get; set; }
}
