using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class LedgerAccount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "GBP";
    public bool IsControlAccount { get; set; }
    public bool IsActive { get; set; } = true;
}
