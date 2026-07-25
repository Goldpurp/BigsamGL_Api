using Application.DTOs;

namespace Application.Common.Interfaces;

public interface IIdentityService
{
    Task<AuthTokenResponse> RegisterCustomerAsync(RegisterCustomerRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthTokenResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<AuthTokenResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
    Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken);
    Task<AccountActionResponse> RequestEmailVerificationAsync(Guid userId, CancellationToken cancellationToken);
    Task VerifyEmailAsync(string token, CancellationToken cancellationToken);
    Task<AccountActionResponse> RequestPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}

public interface ICurrentUserService
{
    string? UserId { get; }
    string? IpAddress { get; }
}
