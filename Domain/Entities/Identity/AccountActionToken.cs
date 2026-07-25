using Domain.Common;

namespace Domain.Entities;

public class AccountActionToken : BaseEntity
{
    public Guid UserAccountId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
