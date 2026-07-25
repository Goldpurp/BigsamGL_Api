using Api.Services;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("api/notifications")]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    public NotificationsController(IApplicationDbContext context, IActiveBranchContext branches)
    {
        _context = context;
        _branches = branches;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] bool? unread,
        [FromQuery] string? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.StaffNotifications.AsNoTracking().Where(x =>
            x.UserAccountId == CurrentUserId &&
            (x.BranchId == null || branch.IsAllBranches || x.BranchId == branch.BranchId));
        if (unread == true) query = query.Where(x => x.ReadAt == null);
        if (unread == false) query = query.Where(x => x.ReadAt != null);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var value = category.Trim().ToLowerInvariant();
            query = query.Where(x => x.Category.ToLower() == value);
        }
        return Ok(await query.OrderByDescending(x => x.IsUrgent).ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id).ToPagedResponseAsync(page, pageSize, true, cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var count = await _context.StaffNotifications.AsNoTracking().CountAsync(x =>
            x.UserAccountId == CurrentUserId && x.ReadAt == null &&
            (x.BranchId == null || branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken);
        return Ok(new { Count = count });
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await _context.StaffNotifications.FirstOrDefaultAsync(x =>
            x.Id == notificationId && x.UserAccountId == CurrentUserId, cancellationToken);
        if (notification is null) return NotFound();
        notification.ReadAt ??= DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var now = DateTime.UtcNow;
        await _context.StaffNotifications.Where(x => x.UserAccountId == CurrentUserId &&
                x.ReadAt == null && (x.BranchId == null || branch.IsAllBranches ||
                                     x.BranchId == branch.BranchId))
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ReadAt, now), cancellationToken);
        return NoContent();
    }

    [HttpGet("settings")]
    [HttpGet("/api/notification-preferences")]
    public async Task<ActionResult> Settings(CancellationToken cancellationToken)
    {
        var settings = await _context.UserNotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserAccountId == CurrentUserId, cancellationToken);
        return Ok(settings ?? new UserNotificationPreference { UserAccountId = CurrentUserId });
    }

    [HttpPut("settings")]
    [HttpPatch("/api/notification-preferences")]
    public async Task<ActionResult> UpdateSettings(UpdateNotificationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _context.UserNotificationPreferences.FirstOrDefaultAsync(x =>
            x.UserAccountId == CurrentUserId, cancellationToken);
        if (settings is null)
        {
            settings = new UserNotificationPreference { UserAccountId = CurrentUserId };
            _context.UserNotificationPreferences.Add(settings);
        }
        settings.InAppEnabled = request.InAppEnabled;
        settings.EmailEnabled = request.EmailEnabled;
        settings.ShipmentAlerts = request.ShipmentAlerts;
        settings.FinanceAlerts = request.FinanceAlerts;
        settings.SecurityAlerts = request.SecurityAlerts;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(settings);
    }
}

public sealed record UpdateNotificationSettingsRequest(bool InAppEnabled, bool EmailEnabled,
    bool ShipmentAlerts, bool FinanceAlerts, bool SecurityAlerts);
