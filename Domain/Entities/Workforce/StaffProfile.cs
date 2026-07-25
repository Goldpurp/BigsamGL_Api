using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class StaffProfile : BaseEntity
{
    public Guid UserAccountId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public StaffStatus Status { get; set; } = StaffStatus.Active;
    public string? StatusReason { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }
}
