using Domain.Common;

namespace Domain.Entities;

public class BankAccount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "GBP";
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}
