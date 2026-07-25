using Domain.Common;

namespace Domain.Entities;

public class SalesTarget : BaseEntity
{
    public Guid StaffUserId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal RevenueTarget { get; set; }
    public int WonLeadTarget { get; set; }
    public string Currency { get; set; } = "GBP";
}
