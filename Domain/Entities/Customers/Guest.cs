using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Guest : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string Source { get; set; } = "Website";
    public string ServiceInterest { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public GuestStatus Status { get; set; } = GuestStatus.New;
    public Guid? AssignedToUserId { get; set; }
    public Guid? ConvertedCustomerId { get; set; }
    public DateTime? LastContactedAt { get; set; }
    public DateTime? ConvertedAt { get; set; }
}
