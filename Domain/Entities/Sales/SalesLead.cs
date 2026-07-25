using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class SalesLead : BaseEntity
{
    public Guid? CustomerId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string ServiceInterest { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public LeadStage Stage { get; set; } = LeadStage.New;
    public LeadPriority Priority { get; set; } = LeadPriority.Normal;
    public string Source { get; set; } = "Website";
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime? NextActionAt { get; set; }
    public string? Notes { get; set; }
    public string? LostReason { get; set; }
    public DateTime? LastContactedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ClosedAt { get; set; }
}
