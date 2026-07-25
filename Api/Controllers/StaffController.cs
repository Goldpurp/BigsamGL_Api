using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/staff")]
[Route("api/admin/staff")]
public sealed class StaffController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public StaffController(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    [HttpPost]
    public async Task<ActionResult<StaffDto>> Create(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var staff = await _identityService.CreateStaffAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { userId = staff.UserId }, staff);
    }

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] string? search, [FromQuery] string? branchCode,
        [FromQuery] StaffStatus? status, [FromQuery] bool paged = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query =
            from profile in _context.StaffProfiles.AsNoTracking()
            join user in _context.UserAccounts.AsNoTracking() on profile.UserAccountId equals user.Id
            select new { Profile = profile, User = user };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.User.FullName.ToLower().Contains(term) ||
                x.User.Email.ToLower().Contains(term) || x.Profile.EmployeeNumber.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(branchCode))
            query = query.Where(x => x.Profile.BranchCode == branchCode.Trim().ToUpperInvariant());
        if (status.HasValue) query = query.Where(x => x.Profile.Status == status.Value);
        var ordered = query.OrderBy(x => x.User.FullName).ThenBy(x => x.User.Id)
            .Select(x => new StaffDto(x.User.Id, x.User.FullName, x.User.Email, x.User.Role, x.User.Status,
                x.Profile.EmployeeNumber, x.Profile.JobTitle, x.Profile.Department, x.Profile.BranchCode,
                x.Profile.HireDate, x.Profile.Status, x.Profile.Id,
                x.Profile.StatusReason, x.Profile.ArchivedAt));
        if (paged) return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
        return Ok(await ordered.Take(500).ToListAsync(cancellationToken));
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<StaffDto>> GetById(Guid userId, CancellationToken cancellationToken)
    {
        var result = await (
            from profile in _context.StaffProfiles.AsNoTracking()
            join user in _context.UserAccounts.AsNoTracking() on profile.UserAccountId equals user.Id
            where user.Id == userId
            select new StaffDto(user.Id, user.FullName, user.Email, user.Role, user.Status,
                profile.EmployeeNumber, profile.JobTitle, profile.Department, profile.BranchCode,
                profile.HireDate, profile.Status, profile.Id,
                profile.StatusReason, profile.ArchivedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{userId:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid userId, ChangeStaffStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Staff account not found.");
        var profile = await _context.StaffProfiles.FirstOrDefaultAsync(x => x.UserAccountId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Staff profile not found.");
        var error = await ValidateLifecycleChangeAsync(user, request.AccountStatus, request.StaffStatus,
            cancellationToken);
        if (error is not null) return Conflict(error);
        if (request.AccountStatus != AccountStatus.Active && string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("A reason is required when restricting a staff account.");
        user.Status = request.AccountStatus;
        profile.Status = request.StaffStatus;
        profile.StatusReason = request.Reason?.Trim();
        if (request.AccountStatus == AccountStatus.Active)
        {
            profile.ArchivedAt = null;
            profile.ArchivedByUserId = null;
        }
        else await RevokeSessionsAsync(user.Id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid userId, StaffLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("A suspension reason is required.");
        return await SetLifecycleAsync(userId, AccountStatus.Suspended, StaffStatus.Suspended,
            request.Reason, false, cancellationToken);
    }

    [HttpPost("{userId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid userId, StaffLifecycleRequest request,
        CancellationToken cancellationToken) =>
        await SetLifecycleAsync(userId, AccountStatus.Active, StaffStatus.Active,
            string.IsNullOrWhiteSpace(request.Reason) ? "Reactivated by administrator." : request.Reason,
            false, cancellationToken);

    [HttpDelete("{userId:guid}")]
    [HttpPost("{userId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid userId, [FromQuery] string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest("A termination/archive reason is required. Historical staff records are retained.");
        return await SetLifecycleAsync(userId, AccountStatus.Disabled, StaffStatus.Terminated,
            reason, true, cancellationToken);
    }

    [HttpPut("{userId:guid}")]
    [HttpPatch("{userId:guid}")]
    public async Task<ActionResult<StaffDto>> Update(Guid userId, UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        if (request.Role == UserRole.Customer) return BadRequest("Staff cannot use the Customer role.");
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Staff account not found.");
        var profile = await _context.StaffProfiles.FirstOrDefaultAsync(x => x.UserAccountId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Staff profile not found.");
        if (request.AccountStatus != user.Status || request.StaffStatus != profile.Status)
            return BadRequest("Use the staff status, suspend, reactivate, or delete endpoint for lifecycle changes.");
        if (userId == CurrentUserId && (request.Role != user.Role || request.AccountStatus != user.Status ||
                request.StaffStatus != profile.Status))
            return Conflict("Administrators cannot change their own role or lifecycle status.");
        if (user.Role == UserRole.SuperAdmin && request.Role != UserRole.SuperAdmin &&
            await CountActiveSuperAdminsAsync(cancellationToken) <= 1)
            return Conflict("The final active SuperAdmin cannot be demoted.");
        user.FullName = request.FullName.Trim(); user.Role = request.Role; user.Status = request.AccountStatus;
        profile.JobTitle = request.JobTitle.Trim(); profile.Department = request.Department.Trim();
        profile.BranchCode = request.BranchCode.Trim().ToUpperInvariant(); profile.Status = request.StaffStatus;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new StaffDto(user.Id, user.FullName, user.Email, user.Role, user.Status, profile.EmployeeNumber,
            profile.JobTitle, profile.Department, profile.BranchCode, profile.HireDate, profile.Status,
            profile.Id, profile.StatusReason, profile.ArchivedAt));
    }

    [HttpPost("{userId:guid}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(Guid userId, CancellationToken cancellationToken)
    {
        if (!await _context.UserAccounts.AnyAsync(x => x.Id == userId &&
            x.Role != UserRole.Customer, cancellationToken)) return NotFound();
        await RevokeSessionsAsync(userId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("roles")]
    [HttpGet("/api/admin/roles")]
    public ActionResult Roles() => Ok(new[]
    {
        new { Role = nameof(UserRole.SuperAdmin), Scope = "All branches and all administration" },
        new { Role = nameof(UserRole.Administrator), Scope = "Staff, company settings and assigned branches" },
        new { Role = nameof(UserRole.OperationsManager), Scope = "Shipments, manifests, tracking, schedule and shared prices" },
        new { Role = nameof(UserRole.SalesManager), Scope = "Clients, quotations, shipments, invoices and shared prices" },
        new { Role = nameof(UserRole.FinanceManager), Scope = "Finance, accounting, purchases, reports, invoices and shared prices" },
        new { Role = nameof(UserRole.Staff), Scope = "Assigned operational duties, shipments and shared prices" }
    });

    [HttpGet("/api/admin/permissions")]
    public ActionResult Permissions() => Ok(new[]
    {
        new { Group = "Core", Values = new[] { "overview:read", "search:use", "notification:read",
            "client:read", "shipment:read", "shipment:create", "shipment:update", "shipment:cancel",
            "sales:read", "price:manage", "schedule:read", "schedule:manage", "pickup:manage",
            "manifest:read", "report:read" } },
        new { Group = "Operations", Values = new[] { "client:create", "client:update", "shipment:create",
            "shipment:update", "shipment:cancel", "pickup:manage", "manifest:manage", "tracking:manage",
            "tracking:publish", "cargo-issue:manage", "inventory:manage" } },
        new { Group = "Finance", Values = new[] { "receipt:read", "credit-note:issue", "credit-note:apply",
            "supplier:manage", "purchase:manage", "finance:read", "finance:post", "finance:reverse",
            "accounting:read", "accounting:adjust", "report:financial" } },
        new { Group = "Administration", Values = new[] { "staff:read", "staff:manage", "director:read",
            "admin:manage", "audit:read", "integration:manage", "import:manage", "report:personal-data" } }
    });

    private async Task<IActionResult> SetLifecycleAsync(Guid userId, AccountStatus accountStatus,
        StaffStatus staffStatus, string reason, bool archive, CancellationToken cancellationToken)
    {
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Staff account not found.");
        var profile = await _context.StaffProfiles.FirstOrDefaultAsync(x => x.UserAccountId == userId,
            cancellationToken) ?? throw new KeyNotFoundException("Staff profile not found.");
        var error = await ValidateLifecycleChangeAsync(user, accountStatus, staffStatus, cancellationToken);
        if (error is not null) return Conflict(error);
        user.Status = accountStatus;
        profile.Status = staffStatus;
        profile.StatusReason = reason.Trim();
        if (archive)
        {
            profile.ArchivedAt = DateTime.UtcNow;
            profile.ArchivedByUserId = CurrentUserId;
        }
        else if (accountStatus == AccountStatus.Active)
        {
            profile.ArchivedAt = null;
            profile.ArchivedByUserId = null;
        }
        if (accountStatus != AccountStatus.Active) await RevokeSessionsAsync(user.Id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<string?> ValidateLifecycleChangeAsync(Domain.Entities.UserAccount user,
        AccountStatus accountStatus, StaffStatus staffStatus, CancellationToken cancellationToken)
    {
        if (user.Id == CurrentUserId && accountStatus != AccountStatus.Active)
            return "Administrators cannot suspend or archive their own account.";
        if (user.Role == UserRole.SuperAdmin && accountStatus != AccountStatus.Active &&
            await CountActiveSuperAdminsAsync(cancellationToken) <= 1)
            return "The final active SuperAdmin cannot be suspended or archived.";
        if (accountStatus == AccountStatus.Active && staffStatus is StaffStatus.Suspended or StaffStatus.Terminated)
            return "An active account cannot have a suspended or terminated staff profile.";
        if (accountStatus == AccountStatus.Suspended && staffStatus != StaffStatus.Suspended)
            return "A suspended account must have a suspended staff profile.";
        if (accountStatus == AccountStatus.Disabled && staffStatus != StaffStatus.Terminated)
            return "A disabled account must have a terminated staff profile.";
        return null;
    }

    private Task<int> CountActiveSuperAdminsAsync(CancellationToken cancellationToken) =>
        _context.UserAccounts.CountAsync(x => x.Role == UserRole.SuperAdmin && x.Status == AccountStatus.Active,
            cancellationToken);

    private async Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tokens = await _context.RefreshTokens.Where(x => x.UserAccountId == userId && !x.RevokedAt.HasValue)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens) token.RevokedAt = now;
    }
}
