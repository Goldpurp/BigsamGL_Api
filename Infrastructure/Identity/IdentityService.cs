using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Application.Common;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private const int MaximumFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly IApplicationDbContext _context;
    private readonly JwtOptions _options;
    private readonly IIntegrationGateway _integrations;
    private readonly ICompanyDocumentNumberGenerator? _documentNumbers;
    private readonly bool _returnLocalActionToken;
    private readonly string _publicSiteBaseUrl;
    private readonly string _defaultBranchCode;

    public IdentityService(IApplicationDbContext context, IOptions<JwtOptions> options,
        IIntegrationGateway integrations,
        ICompanyDocumentNumberGenerator? documentNumbers = null,
        IConfiguration? configuration = null)
    {
        _context = context;
        _options = options.Value;
        _integrations = integrations;
        _documentNumbers = documentNumbers;
        _returnLocalActionToken = string.Equals(configuration?["Integrations:Mode"], "Local",
            StringComparison.OrdinalIgnoreCase);
        _publicSiteBaseUrl = (configuration?["PublicSite:BaseUrl"] ??
            "http://127.0.0.1:3001").TrimEnd('/');
        _defaultBranchCode = (configuration?["CustomerPortal:DefaultBranchCode"] ??
            "UK-MAN").Trim().ToUpperInvariant();
    }

    public async Task<AuthTokenResponse> RegisterCustomerAsync(
        RegisterCustomerRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        ValidatePassword(request.Password);
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedName = ClientIdentityNormalizer.Name(request.FullName);
        if (await _context.UserAccounts.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new InvalidOperationException("An account already exists for this email address.");
        if (await _context.UserAccounts.AnyAsync(x => x.NormalizedFullName == normalizedName, cancellationToken))
            throw new InvalidOperationException("An account already exists for this full name. Sign in or contact support.");

        var customer = await _context.Customers.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (customer is not null && customer.NormalizedFullName != normalizedName)
            throw new InvalidOperationException("These details match an existing client record. Contact support to verify ownership.");
        if (customer is null && await _context.Customers.AnyAsync(x => x.NormalizedFullName == normalizedName, cancellationToken))
            throw new InvalidOperationException("These details match an existing client record. Contact support to verify ownership.");
        var isNewCustomer = customer is null;
        customer ??= new Customer(Guid.NewGuid(), request.FullName.Trim(), normalizedEmail.ToLowerInvariant(), 0, request.CountryCode);
        if (isNewCustomer)
        {
            var country = request.CountryCode.Trim().ToUpperInvariant();
            var branch = await _context.Branches.AsNoTracking()
                .Where(x => x.IsActive &&
                    (x.CountryCode == country || x.Code == _defaultBranchCode))
                .OrderByDescending(x => x.CountryCode == country)
                .ThenByDescending(x => x.Code == _defaultBranchCode)
                .ThenBy(x => x.Code)
                .Select(x => new { x.Id, x.CountryCode })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Customer registration is unavailable until a default BiGSAM branch is configured.");
            customer.BranchId = branch.Id;
            customer.ClientNumber = _documentNumbers is null
                ? $"ID-{Guid.NewGuid():N}"[..15].ToUpperInvariant()
                : await _documentNumbers.NextClientAsync(cancellationToken);
            customer.WalletCurrency = CountryCurrency.ForCountry(branch.CountryCode);
        }
        customer.FullName = request.FullName.Trim();
        customer.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        var user = new UserAccount
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            NormalizedEmail = normalizedEmail,
            FullName = request.FullName.Trim(),
            NormalizedFullName = normalizedName,
            PasswordHash = PasswordService.Hash(request.Password),
            Role = UserRole.Customer,
            Status = AccountStatus.Active,
            CustomerId = customer.Id
        };

        if (isNewCustomer) _context.Customers.Add(customer);
        if (!await _context.NotificationPreferences.AnyAsync(x => x.CustomerId == customer.Id, cancellationToken))
            _context.NotificationPreferences.Add(new NotificationPreference { CustomerId = customer.Id });
        _context.UserAccounts.Add(user);
        var result = CreateTokenResponse(user, ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        var now = DateTime.UtcNow;

        if (user is null || !user.CanSignIn(now))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!PasswordService.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaximumFailedLogins)
            {
                user.LockedUntil = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        var result = CreateTokenResponse(user, ipAddress);
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<AuthTokenResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (stored is null)
            throw new UnauthorizedAccessException("The refresh token is invalid or expired.");
        if (stored.RevokedAt.HasValue && !string.IsNullOrWhiteSpace(stored.ReplacedByTokenHash))
        {
            await RevokeUserSessionsAsync(stored.UserAccountId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token reuse was detected. All sessions were revoked.");
        }
        if (!stored.IsActive(DateTime.UtcNow))
            throw new UnauthorizedAccessException("The refresh token is invalid or expired.");

        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == stored.UserAccountId, cancellationToken);
        if (user is null || !user.CanSignIn(DateTime.UtcNow))
            throw new UnauthorizedAccessException("This account cannot sign in.");

        stored.RevokedAt = DateTime.UtcNow;
        var result = CreateTokenResponse(user, ipAddress);
        stored.ReplacedByTokenHash = HashToken(result.RefreshToken);
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (stored is null || stored.RevokedAt.HasValue) return;
        stored.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        if (request.Role == UserRole.Customer)
            throw new ArgumentException("A staff account cannot use the Customer role.");

        ValidatePassword(request.TemporaryPassword);
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedName = ClientIdentityNormalizer.Name(request.FullName);
        if (await _context.UserAccounts.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new InvalidOperationException("An account already exists for this email address.");
        if (await _context.UserAccounts.AnyAsync(x => x.NormalizedFullName == normalizedName, cancellationToken))
            throw new InvalidOperationException("A user account already exists for this full name.");
        if (await _context.StaffProfiles.AnyAsync(x => x.EmployeeNumber == request.EmployeeNumber.Trim(), cancellationToken))
            throw new InvalidOperationException("The employee number is already in use.");

        var user = new UserAccount
        {
            FullName = request.FullName.Trim(),
            NormalizedFullName = normalizedName,
            Email = request.Email.Trim().ToLowerInvariant(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = PasswordService.Hash(request.TemporaryPassword),
            Role = request.Role,
            Status = AccountStatus.Active,
            MustChangePassword = true
        };
        var staff = new StaffProfile
        {
            UserAccountId = user.Id,
            EmployeeNumber = request.EmployeeNumber.Trim().ToUpperInvariant(),
            JobTitle = request.JobTitle.Trim(),
            Department = request.Department.Trim(),
            BranchCode = request.BranchCode.Trim().ToUpperInvariant(),
            HireDate = request.HireDate.Date,
            Status = StaffStatus.Active
        };

        _context.UserAccounts.Add(user);
        _context.StaffProfiles.Add(staff);
        await _context.SaveChangesAsync(cancellationToken);
        return new StaffDto(user.Id, user.FullName, user.Email, user.Role, user.Status,
            staff.EmployeeNumber, staff.JobTitle, staff.Department, staff.BranchCode, staff.HireDate, staff.Status,
            staff.Id);
    }

    public async Task<AccountActionResponse> RequestEmailVerificationAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("User account not found.");
        if (user.EmailVerifiedAt.HasValue) return new AccountActionResponse("Email is already verified.", null, null);
        return await CreateActionTokenAsync(user, "EmailVerification", TimeSpan.FromHours(24), cancellationToken);
    }

    public async Task VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        var action = await GetActiveActionTokenAsync(token, "EmailVerification", cancellationToken);
        var user = await _context.UserAccounts.FirstAsync(x => x.Id == action.UserAccountId, cancellationToken);
        user.EmailVerifiedAt = DateTime.UtcNow; action.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AccountActionResponse> RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.NormalizedEmail == normalized, cancellationToken);
        if (user is null) return new AccountActionResponse("If the account exists, reset instructions have been created.", null, null);
        return await CreateActionTokenAsync(user, "PasswordReset", TimeSpan.FromHours(1), cancellationToken);
    }

    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword); var action = await GetActiveActionTokenAsync(token, "PasswordReset", cancellationToken);
        var user = await _context.UserAccounts.FirstAsync(x => x.Id == action.UserAccountId, cancellationToken);
        user.PasswordHash = PasswordService.Hash(newPassword); user.MustChangePassword = false; user.FailedLoginCount = 0; user.LockedUntil = null;
        action.UsedAt = DateTime.UtcNow; await RevokeUserSessionsAsync(user.Id, cancellationToken); await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword); var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("User account not found.");
        if (!PasswordService.Verify(currentPassword, user.PasswordHash)) throw new UnauthorizedAccessException("Current password is incorrect.");
        user.PasswordHash = PasswordService.Hash(newPassword); user.MustChangePassword = false;
        await RevokeUserSessionsAsync(user.Id, cancellationToken); await _context.SaveChangesAsync(cancellationToken);
    }

    private AuthTokenResponse CreateTokenResponse(UserAccount user, string? ipAddress)
    {
        if (Encoding.UTF8.GetByteCount(_options.Secret) < 32)
            throw new InvalidOperationException("JWT secret must be at least 32 bytes.");

        var now = DateTime.UtcNow;
        var accessExpiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName),
            new("role", user.Role.ToString())
        };
        if (user.CustomerId.HasValue)
            claims.Add(new Claim("customer_id", user.CustomerId.Value.ToString()));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now,
            accessExpiresAt,
            credentials);

        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserAccountId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            ExpiresAt = now.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ipAddress
        });

        return new AuthTokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            rawRefreshToken,
            accessExpiresAt,
            new AuthenticatedUserDto(user.Id, user.FullName, user.Email, user.Role, user.CustomerId,
                user.EmailVerifiedAt.HasValue, user.MustChangePassword));
    }

    private async Task<AccountActionResponse> CreateActionTokenAsync(UserAccount user, string purpose, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)); var expires = DateTime.UtcNow.Add(lifetime);
        var mode = purpose == "EmailVerification" ? "verify" : "reset";
        var actionUrl = $"{_publicSiteBaseUrl}/#/dashboard?mode={mode}&token={Uri.EscapeDataString(raw)}";
        _context.AccountActionTokens.Add(new AccountActionToken { UserAccountId = user.Id, Purpose = purpose,
            TokenHash = HashToken(raw), ExpiresAt = expires });
        await _integrations.SendNotificationAsync(IntegrationKind.Email, user.Email, purpose,
            new { user.FullName, ActionUrl = actionUrl, ExpiresAt = expires }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return new AccountActionResponse(
            purpose == "EmailVerification"
                ? "Check your email to verify your account."
                : "If the account exists, check your email for reset steps.",
            _returnLocalActionToken ? raw : null,
            expires);
    }

    private async Task<AccountActionToken> GetActiveActionTokenAsync(string rawToken, string purpose, CancellationToken cancellationToken)
    {
        var hash = HashToken(rawToken); return await _context.AccountActionTokens.FirstOrDefaultAsync(x => x.TokenHash == hash &&
            x.Purpose == purpose && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow, cancellationToken)
            ?? throw new UnauthorizedAccessException("Action token is invalid or expired.");
    }

    private async Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await _context.RefreshTokens.Where(x => x.UserAccountId == userId && x.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var refresh in tokens) refresh.RevokedAt = DateTime.UtcNow;
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("A valid email address is required.");
        return email.Trim().ToUpperInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 12 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
            throw new ArgumentException("Password must be at least 12 characters and include upper-case, lower-case, number, and symbol characters.");
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
