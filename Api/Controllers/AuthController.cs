using System.Security.Claims;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Domain.Enums;
using Api.Services;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;

namespace Api.Controllers;

[Route("api/auth")]
[EnableRateLimiting("Authentication")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IStaffActivityService _staffActivity;
    private readonly IActiveBranchContext _branches;

    public AuthController(IIdentityService identityService, IApplicationDbContext context,
        IWebHostEnvironment environment, IStaffActivityService staffActivity,
        IActiveBranchContext branches)
    {
        _identityService = identityService;
        _context = context;
        _environment = environment;
        _staffActivity = staffActivity;
        _branches = branches;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthSessionResponse>> Register(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.RegisterCustomerAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(CreateSession(result));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthSessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            var result = await _identityService.LoginAsync(request,
                HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            if (result.User.Role != UserRole.Customer)
                await RecordAuthenticationAsync(result.User.Id, "Authentication.Login", StatusCodes.Status200OK,
                    StaffActivityOutcome.Succeeded, timer.ElapsedMilliseconds, ReadSessionId(result.AccessToken), cancellationToken);
            return Ok(CreateSession(result));
        }
        catch (UnauthorizedAccessException)
        {
            var normalizedEmail = request.Email.Trim().ToUpperInvariant();
            var staff = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x =>
                x.NormalizedEmail == normalizedEmail && x.Role != UserRole.Customer, cancellationToken);
            if (staff is not null)
                await RecordAuthenticationAsync(staff.Id, "Authentication.Login", StatusCodes.Status401Unauthorized,
                    StaffActivityOutcome.Unauthorized, timer.ElapsedMilliseconds, string.Empty, cancellationToken);
            throw;
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthSessionResponse>> Refresh([FromBody] RefreshRequest? request, CancellationToken cancellationToken)
    {
        var rawToken = request?.RefreshToken ?? Request.Cookies["bigsam_refresh"]
            ?? throw new UnauthorizedAccessException("Refresh session is missing.");
        var result = await _identityService.RefreshAsync(rawToken, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (result.User.Role != UserRole.Customer)
            await RecordAuthenticationAsync(result.User.Id, "Authentication.Refresh", StatusCodes.Status200OK,
                StaffActivityOutcome.Succeeded, 0, ReadSessionId(result.AccessToken), cancellationToken);
        return Ok(CreateSession(result));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? request, CancellationToken cancellationToken)
    {
        var rawToken = request?.RefreshToken ?? Request.Cookies["bigsam_refresh"];
        if (!string.IsNullOrWhiteSpace(rawToken)) await _identityService.RevokeAsync(rawToken, cancellationToken);
        Response.Cookies.Delete("bigsam_refresh", RefreshCookieOptions());
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [HttpGet("/api/me")]
    public async Task<ActionResult<AuthMeResponse>> Me(CancellationToken cancellationToken)
    {
        var id = CurrentUserId;
        var user = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("User account not found.");
        var employeeNumber = await _context.StaffProfiles.AsNoTracking()
            .Where(x => x.UserAccountId == id).Select(x => x.EmployeeNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var active = await _branches.ResolveAsync(false, cancellationToken);
        var membershipRows = await (from membership in _context.StaffBranchMemberships.AsNoTracking()
                                    join branch in _context.Branches.AsNoTracking()
                                        on membership.BranchId equals branch.Id
                                    where membership.UserAccountId == id && branch.IsActive
                                    orderby membership.IsPrimary descending, branch.Code
                                    select new { Branch = branch, membership.IsPrimary })
            .ToListAsync(cancellationToken);
        var memberships = membershipRows.Select(x => new AuthBranchMembershipResponse(x.Branch.Id,
            BranchKey(x.Branch.Code), x.Branch.Name, CountryCurrency.ForCountry(x.Branch.CountryCode),
            x.Branch.TimeZone, x.IsPrimary)).ToList();
        if (memberships.Count == 0 && user.Role == UserRole.SuperAdmin)
        {
            var allBranches = await _context.Branches.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.Code).ToListAsync(cancellationToken);
            memberships = allBranches.Select(x => new AuthBranchMembershipResponse(x.Id,
                BranchKey(x.Code), x.Name, CountryCurrency.ForCountry(x.CountryCode), x.TimeZone, false))
                .ToList();
        }
        return Ok(new AuthMeResponse(new AuthenticatedUserDto(user.Id, user.FullName, user.Email, user.Role,
            user.CustomerId, user.EmailVerifiedAt.HasValue, user.MustChangePassword), memberships,
            active.BranchKey ?? "all", active.IsAllBranches, Permissions(user.Role),
            employeeNumber ?? string.Empty));
    }

    [Authorize(Policy = "StaffOnly")]
    [HttpGet("/api/me/branch-scope")]
    public async Task<ActionResult> BranchScope(CancellationToken cancellationToken)
    {
        var scope = await _branches.ResolveAsync(false, cancellationToken);
        var memberships = await (from membership in _context.StaffBranchMemberships.AsNoTracking()
                                 join branch in _context.Branches.AsNoTracking()
                                     on membership.BranchId equals branch.Id
                                 where membership.UserAccountId == CurrentUserId && branch.IsActive
                                 orderby membership.IsPrimary descending, branch.Code
                                 select new
                                 {
                                     branch.Id, branch.Code, branch.Name, branch.CountryCode,
                                     branch.TimeZone, membership.IsPrimary
                                 }).ToListAsync(cancellationToken);
        var role = await _context.UserAccounts.AsNoTracking().Where(x => x.Id == CurrentUserId)
            .Select(x => x.Role).SingleAsync(cancellationToken);
        if (role == UserRole.SuperAdmin && memberships.Count == 0)
            memberships = await _context.Branches.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.Code).Select(x => new
                {
                    x.Id, x.Code, x.Name, x.CountryCode, x.TimeZone,
                    IsPrimary = false
                }).ToListAsync(cancellationToken);
        return Ok(new
        {
            ActiveBranch = scope.IsAllBranches ? "all" : scope.Code,
            scope.IsAllBranches,
            CanSelectAll = role == UserRole.SuperAdmin,
            Branches = memberships
        });
    }

    [Authorize(Policy = "StaffOnly")]
    [HttpPatch("/api/me/active-branch")]
    public async Task<ActionResult> ActiveBranch(ChangeActiveBranchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Branch))
            return UnprocessableEntity(new { code = "BRANCH_REQUIRED",
                detail = "Choose all, UK-MAN, NG-LOS or CN-GZ." });
        Request.Headers["X-Active-Branch"] = request.Branch.Trim();
        var scope = await _branches.ResolveAsync(false, cancellationToken);
        return Ok(new
        {
            ActiveBranch = scope.IsAllBranches ? "all" : scope.Code,
            scope.BranchId, scope.Name, scope.CountryCode, scope.City, scope.Currency,
            scope.TimeZone, scope.IsAllBranches,
            Header = new { Name = "X-Active-Branch",
                Value = scope.IsAllBranches ? "all" : scope.Code }
        });
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<AuthSessionItemResponse>>> Sessions(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await _context.RefreshTokens.AsNoTracking().Where(x =>
                x.UserAccountId == CurrentUserId && x.RevokedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AuthSessionItemResponse(x.Id, x.CreatedAt, x.ExpiresAt, x.CreatedByIp))
            .ToListAsync(cancellationToken);
        return Ok(sessions);
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Id == sessionId &&
            x.UserAccountId == CurrentUserId, cancellationToken);
        if (session is null) return NotFound();
        if (!session.RevokedAt.HasValue)
        {
            session.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [Authorize]
    [HttpPost("email-verification/request")]
    public async Task<ActionResult<AccountActionResponse>> RequestEmailVerification(CancellationToken cancellationToken) =>
        Ok(await _identityService.RequestEmailVerificationAsync(CurrentUserId, cancellationToken));

    [AllowAnonymous]
    [HttpPost("email-verification/confirm")]
    public async Task<IActionResult> VerifyEmail([FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        await _identityService.VerifyEmailAsync(request.Token, cancellationToken); return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<AccountActionResponse>> RequestPasswordReset([FromBody] PasswordResetEmailRequest request, CancellationToken cancellationToken) =>
        Ok(await _identityService.RequestPasswordResetAsync(request.Email, cancellationToken));

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _identityService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken); return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _identityService.ChangePasswordAsync(CurrentUserId, request.CurrentPassword, request.NewPassword, cancellationToken); return NoContent();
    }

    private AuthSessionResponse CreateSession(AuthTokenResponse result)
    {
        var options = RefreshCookieOptions();
        options.Expires = DateTimeOffset.UtcNow.AddDays(30);
        Response.Cookies.Append("bigsam_refresh", result.RefreshToken, options);
        return new AuthSessionResponse(result.AccessToken, result.AccessTokenExpiresAt, result.User);
    }

    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        // Local is a first-class HTTP development environment. Production
        // remains HTTPS-only, while Local/Development can keep the refresh
        // cookie through the Vite proxy.
        Secure = _environment.IsProduction(),
        SameSite = SameSiteMode.Strict,
        IsEssential = true,
        Path = "/api/auth"
    };

    private async Task RecordAuthenticationAsync(Guid userId, string activityType, int statusCode,
        StaffActivityOutcome outcome, long durationMilliseconds, string sessionId,
        CancellationToken cancellationToken)
    {
        var correlationId = Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 100 ||
            correlationId.Any(x => !char.IsLetterOrDigit(x) && x is not ('-' or '_' or '.' or ':')))
            correlationId = HttpContext.TraceIdentifier;
        Response.Headers["X-Correlation-ID"] = correlationId;
        await _staffActivity.RecordAsync(new StaffActivityInput(userId, activityType, Request.Method,
            $"/api/auth/{(activityType.EndsWith("Refresh", StringComparison.Ordinal) ? "refresh" : "login")}",
            Request.Path, string.Empty, nameof(AuthController),
            activityType.EndsWith("Refresh", StringComparison.Ordinal) ? nameof(Refresh) : nameof(Login),
            statusCode, outcome, durationMilliseconds,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty,
            Request.Headers["X-Device-ID"].FirstOrDefault() ?? string.Empty,
            correlationId, sessionId, DateTime.UtcNow,
            StaffLocationHeaderParser.Read(Request.Headers)), cancellationToken);
    }

    private static string ReadSessionId(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims
            .FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value ?? string.Empty;

    private static string BranchKey(string code) => code switch
    {
        "UK-MAN" => "manchester", "NG-LOS" => "lagos", "CN-GZ" => "guangzhou",
        _ => code.ToLowerInvariant()
    };

    private static IReadOnlyList<string> Permissions(UserRole role)
    {
        string[] common = ["overview:read", "search:use", "notification:read", "client:read",
            "shipment:read", "shipment:create", "shipment:update", "shipment:cancel",
            "sales:read", "price:manage", "schedule:read", "schedule:manage", "pickup:manage",
            "manifest:read", "report:read"];
        string[] operations = ["client:create", "client:update", "client:history", "shipment:create",
            "shipment:update", "shipment:cancel", "schedule:manage", "pickup:manage", "manifest:manage",
            "tracking:manage", "tracking:publish", "cargo-issue:manage", "inventory:manage"];
        string[] sales = ["client:create", "client:update", "client:history", "shipment:create",
            "shipment:update", "quotation:manage", "invoice:update", "receipt:read", "price:manage"];
        string[] finance = ["receipt:read", "credit-note:issue", "credit-note:apply", "supplier:manage",
            "purchase:manage", "finance:read", "finance:post", "finance:reverse", "accounting:read",
            "accounting:adjust", "report:financial"];
        string[] administration = ["staff:read", "staff:manage", "director:read", "admin:manage",
            "audit:read", "integration:manage", "import:manage", "report:personal-data"];
        return role switch
        {
            UserRole.SuperAdmin or UserRole.Administrator => common.Concat(operations).Concat(sales)
                .Concat(finance).Concat(administration).Distinct().OrderBy(x => x).ToArray(),
            UserRole.OperationsManager => common.Concat(operations).Concat(["staff:read"]).Distinct().ToArray(),
            UserRole.SalesManager => common.Concat(sales).Distinct().ToArray(),
            UserRole.FinanceManager => common.Concat(finance).Distinct().ToArray(),
            UserRole.Staff => common.Concat(["client:create", "shipment:create", "shipment:update",
                "quotation:manage", "receipt:read", "schedule:manage", "manifest:manage",
                "tracking:manage"]).Distinct().ToArray(),
            _ => []
        };
    }
}

public sealed record AuthBranchMembershipResponse(Guid Id, string BranchId, string Name,
    string Currency, string TimeZone, bool IsPrimary);
public sealed record AuthMeResponse(AuthenticatedUserDto User,
    IReadOnlyList<AuthBranchMembershipResponse> BranchMemberships, string ActiveBranch,
    bool AllBranches, IReadOnlyList<string> Permissions, string EmployeeNumber);
public sealed record AuthSessionItemResponse(Guid Id, DateTime CreatedAt, DateTime ExpiresAt,
    string? CreatedByIp);
public sealed record ChangeActiveBranchRequest(string Branch);
