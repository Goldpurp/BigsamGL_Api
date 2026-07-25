using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class UserAccount : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string NormalizedFullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Customer;
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public Guid? CustomerId { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public bool MustChangePassword { get; set; }

    public bool CanSignIn(DateTime utcNow) =>
        Status == AccountStatus.Active && (!LockedUntil.HasValue || LockedUntil <= utcNow);
}
