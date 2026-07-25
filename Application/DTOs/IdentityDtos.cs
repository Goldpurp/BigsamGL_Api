using Domain.Enums;

namespace Application.DTOs;

public record RegisterCustomerRequest(
    string FullName,
    string Email,
    string Password,
    string CountryCode);

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string? RefreshToken);
public record TokenRequest(string Token);

public record CreateStaffRequest(
    string FullName,
    string Email,
    string TemporaryPassword,
    UserRole Role,
    string EmployeeNumber,
    string JobTitle,
    string Department,
    string BranchCode,
    DateTime HireDate);

public record AuthenticatedUserDto(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    Guid? CustomerId,
    bool EmailVerified,
    bool MustChangePassword);

public record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    AuthenticatedUserDto User);

public record AuthSessionResponse(string AccessToken, DateTime AccessTokenExpiresAt, AuthenticatedUserDto User);

public record AccountActionResponse(string Message, string? ActionToken, DateTime? ExpiresAt);
public record ResetPasswordRequest(string Token, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
