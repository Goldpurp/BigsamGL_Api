using Domain.Enums;

namespace Api.Controllers;

public record PasswordResetEmailRequest(string Email);
public record ChangeStaffStatusRequest(AccountStatus AccountStatus, StaffStatus StaffStatus, string? Reason = null);
public record StaffLifecycleRequest(string Reason);
public record UpdateStaffRequest(string FullName, UserRole Role, AccountStatus AccountStatus,
    string JobTitle, string Department, string BranchCode, StaffStatus StaffStatus);
