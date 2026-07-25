using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/audit")]
public sealed class AuditController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    public AuditController(IApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult> Search([FromQuery] string? actorId, [FromQuery] string? entityType,
        [FromQuery] string? entityId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string paging = "page", [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(actorId)) query = query.Where(x => x.ActorId == actorId);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(x => x.EntityId == entityId);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to);
        if (string.Equals(paging, "cursor", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                var position = PaginationExtensions.DecodeCursor(cursor);
                query = query.Where(x => x.CreatedAt < position.Timestamp ||
                    x.CreatedAt == position.Timestamp && x.Id.CompareTo(position.Id) < 0);
            }
            var cursorItems = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Take(pageSize + 1).ToListAsync(cancellationToken);
            var hasMore = cursorItems.Count > pageSize;
            if (hasMore) cursorItems.RemoveAt(cursorItems.Count - 1);
            var nextCursor = hasMore && cursorItems.Count > 0
                ? PaginationExtensions.EncodeCursor(cursorItems[^1].CreatedAt, cursorItems[^1].Id) : null;
            return Ok(new CursorPagedResponse<AuditLog>(cursorItems, pageSize, hasMore, nextCursor));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("staff-activity")]
    public async Task<ActionResult> StaffActivity([FromQuery] Guid? staffUserId, [FromQuery] string? branchCode,
        [FromQuery] StaffActivityOutcome? outcome, [FromQuery] string? method, [FromQuery] string? path,
        [FromQuery] bool? hasLocation, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string paging = "page", [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _context.StaffActivityLogs.AsNoTracking().AsQueryable();
        if (staffUserId.HasValue) query = query.Where(x => x.UserAccountId == staffUserId);
        if (!string.IsNullOrWhiteSpace(branchCode))
            query = query.Where(x => x.BranchCode == branchCode.Trim().ToUpper());
        if (outcome.HasValue) query = query.Where(x => x.Outcome == outcome);
        if (!string.IsNullOrWhiteSpace(method)) query = query.Where(x => x.HttpMethod == method.Trim().ToUpper());
        if (!string.IsNullOrWhiteSpace(path)) { var term = path.Trim().ToLower(); query = query.Where(x => x.Path.ToLower().Contains(term)); }
        if (hasLocation == true) query = query.Where(x => x.Latitude.HasValue && x.Longitude.HasValue);
        if (hasLocation == false) query = query.Where(x => !x.Latitude.HasValue || !x.Longitude.HasValue);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to.Value.ToUniversalTime());
        if (string.Equals(paging, "cursor", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                var position = PaginationExtensions.DecodeCursor(cursor);
                query = query.Where(x => x.OccurredAt < position.Timestamp ||
                    x.OccurredAt == position.Timestamp && x.Id.CompareTo(position.Id) < 0);
            }
            var cursorItems = await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
                .Take(pageSize + 1).ToListAsync(cancellationToken);
            var hasMore = cursorItems.Count > pageSize;
            if (hasMore) cursorItems.RemoveAt(cursorItems.Count - 1);
            var nextCursor = hasMore && cursorItems.Count > 0
                ? PaginationExtensions.EncodeCursor(cursorItems[^1].OccurredAt, cursorItems[^1].Id) : null;
            return Ok(new CursorPagedResponse<StaffActivityLog>(cursorItems, pageSize, hasMore, nextCursor));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("staff-activity/{activityId:guid}")]
    public async Task<ActionResult> StaffActivityDetail(Guid activityId, CancellationToken cancellationToken) =>
        Ok(await _context.StaffActivityLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == activityId,
            cancellationToken) ?? throw new KeyNotFoundException("Staff activity record not found."));

    [HttpGet("staff-activity/summary")]
    public async Task<ActionResult> StaffActivitySummary([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var periodTo = (to ?? DateTime.UtcNow).ToUniversalTime();
        var periodFrom = (from ?? periodTo.AddDays(-7)).ToUniversalTime();
        if (periodFrom >= periodTo) return BadRequest("The start date must be before the end date.");
        if (periodTo - periodFrom > TimeSpan.FromDays(366)) return BadRequest("Audit summaries are limited to one year.");
        var activities = await _context.StaffActivityLogs.AsNoTracking().Where(x =>
                x.OccurredAt >= periodFrom && x.OccurredAt <= periodTo)
            .Select(x => new { x.UserAccountId, x.StaffName, x.EmployeeNumber, x.BranchCode,
                x.RouteTemplate, x.Outcome, x.OccurredAt, HasLocation = x.Latitude.HasValue }).ToListAsync(cancellationToken);
        return Ok(new
        {
            PeriodFrom = periodFrom, PeriodTo = periodTo, TotalActions = activities.Count,
            Successful = activities.Count(x => x.Outcome == StaffActivityOutcome.Succeeded),
            FailedOrDenied = activities.Count(x => x.Outcome != StaffActivityOutcome.Succeeded),
            WithLocation = activities.Count(x => x.HasLocation),
            ByStaff = activities.GroupBy(x => new { x.UserAccountId, x.StaffName, x.EmployeeNumber })
                .Select(x => new { x.Key.UserAccountId, x.Key.StaffName, x.Key.EmployeeNumber,
                    Actions = x.Count(), Failures = x.Count(y => y.Outcome != StaffActivityOutcome.Succeeded),
                    LastActionAt = x.Max(y => y.OccurredAt) }).OrderByDescending(x => x.Actions),
            ByBranch = activities.GroupBy(x => x.BranchCode).Select(x => new { BranchCode = x.Key, Actions = x.Count() })
                .OrderByDescending(x => x.Actions),
            ByOutcome = activities.GroupBy(x => x.Outcome).ToDictionary(x => x.Key.ToString(), x => x.Count())
        });
    }

    [HttpGet("staff/{staffUserId:guid}/timeline")]
    public async Task<ActionResult> StaffTimeline(Guid staffUserId, [FromQuery] DateTime? from,
        [FromQuery] DateTime? to, [FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var periodTo = (to ?? DateTime.UtcNow).ToUniversalTime();
        var periodFrom = (from ?? periodTo.AddDays(-30)).ToUniversalTime();
        take = Math.Clamp(take, 1, 500);
        var activities = await _context.StaffActivityLogs.AsNoTracking().Where(x => x.UserAccountId == staffUserId &&
                x.OccurredAt >= periodFrom && x.OccurredAt <= periodTo)
            .OrderByDescending(x => x.OccurredAt).Take(take).ToListAsync(cancellationToken);
        var changes = await _context.AuditLogs.AsNoTracking().Where(x => x.ActorId == staffUserId.ToString() &&
                x.CreatedAt >= periodFrom && x.CreatedAt <= periodTo)
            .OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(cancellationToken);
        return Ok(new { staffUserId, periodFrom, periodTo, activities, dataChanges = changes });
    }

    [HttpGet("locations/history")]
    public async Task<ActionResult> LocationHistory([FromQuery] Guid? staffUserId, [FromQuery] string? branchCode,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int take = 500,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StaffLocationPings.AsNoTracking().AsQueryable();
        if (staffUserId.HasValue) query = query.Where(x => x.UserAccountId == staffUserId);
        if (!string.IsNullOrWhiteSpace(branchCode)) query = query.Where(x => x.BranchCode == branchCode.Trim().ToUpper());
        if (from.HasValue) query = query.Where(x => x.CapturedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.CapturedAt <= to.Value.ToUniversalTime());
        return Ok(await query.OrderByDescending(x => x.CapturedAt).Take(Math.Clamp(take, 1, 2000))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("locations/latest")]
    public async Task<ActionResult> LatestLocations([FromQuery] string? branchCode,
        CancellationToken cancellationToken)
    {
        var query = _context.StaffLocationPings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(branchCode)) query = query.Where(x => x.BranchCode == branchCode.Trim().ToUpper());
        var recent = await query.OrderByDescending(x => x.CapturedAt).Take(5000).ToListAsync(cancellationToken);
        return Ok(recent.GroupBy(x => x.UserAccountId).Select(x => x.First()).OrderBy(x => x.StaffName));
    }
}
