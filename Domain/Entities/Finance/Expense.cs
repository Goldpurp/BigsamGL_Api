using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Expense : BaseEntity
{
    public string ExpenseNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Payee { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime IncurredAt { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;
    public Guid SubmittedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
