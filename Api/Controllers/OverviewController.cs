using Api.Services;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>Small branch-scoped payloads for fast dashboard loading.</summary>
[Authorize(Policy = "StaffOnly")]
[Route("api/overview")]
public sealed class OverviewController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    public OverviewController(IApplicationDbContext context, IActiveBranchContext branches)
    {
        _context = context;
        _branches = branches;
    }

    [HttpGet("summary")]
    public async Task<ActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var scope = await _branches.ResolveAsync(false, cancellationToken);
        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        if (start >= end || end - start > TimeSpan.FromDays(366))
            return BadRequest("Select a valid period of no more than one year.");
        var invoices = await _context.Invoices.AsNoTracking().Where(x =>
                (scope.IsAllBranches || x.BranchId == scope.BranchId) &&
                x.IssueDate >= start && x.IssueDate <= end && x.Status != InvoiceStatus.Void)
            .GroupBy(x => new { x.BranchId, x.IssuerBranchCode, x.Currency })
            .Select(x => new
            {
                x.Key.BranchId, BranchCode = x.Key.IssuerBranchCode, x.Key.Currency,
                InvoiceCount = x.Count(), Sales = x.Sum(y => y.TotalAmount),
                Collected = x.Sum(y => y.AmountPaid),
                Outstanding = x.Sum(y => y.TotalAmount - y.AmountPaid - y.AppliedCreditAmount)
            }).ToListAsync(cancellationToken);
        var activeShipments = await _context.CustomerShipments.AsNoTracking().CountAsync(x =>
            (scope.IsAllBranches || x.BranchId == scope.BranchId) &&
            x.MovementState != CustomerShipmentMovementState.Delivered &&
            x.MovementState != CustomerShipmentMovementState.Cancelled, cancellationToken);
        var openPickups = await _context.ScheduledPickups.AsNoTracking().CountAsync(x =>
            (scope.IsAllBranches || x.BranchId == scope.BranchId) && x.DeletedAt == null &&
            x.State != PickupState.Collected && x.State != PickupState.Cancelled, cancellationToken);
        var unread = await _context.StaffNotifications.AsNoTracking().CountAsync(x =>
            x.UserAccountId == CurrentUserId && x.ReadAt == null &&
            (x.BranchId == null || scope.IsAllBranches || x.BranchId == scope.BranchId),
            cancellationToken);
        return Ok(new { From = start, To = end, GeneratedAt = DateTime.UtcNow,
            FinanceByBranchAndCurrency = invoices, ActiveShipments = activeShipments,
            OpenPickups = openPickups, UnreadNotifications = unread });
    }

    [HttpGet("sales-performance")]
    public async Task<ActionResult> SalesPerformance([FromQuery] int weeks = 12,
        CancellationToken cancellationToken = default)
    {
        var scope = await _branches.ResolveAsync(false, cancellationToken);
        weeks = Math.Clamp(weeks, 1, 52);
        var start = DateTime.UtcNow.Date.AddDays(-(weeks * 7));
        var rows = await _context.Invoices.AsNoTracking().Where(x =>
                (scope.IsAllBranches || x.BranchId == scope.BranchId) &&
                x.IssueDate >= start && x.Status != InvoiceStatus.Void)
            .Select(x => new { x.BranchId, x.IssuerBranchCode, x.Currency, x.IssueDate,
                x.TotalAmount, x.AmountPaid }).ToListAsync(cancellationToken);
        return Ok(rows.GroupBy(x => new
        {
            x.BranchId, BranchCode = x.IssuerBranchCode, x.Currency,
            WeekStart = x.IssueDate.Date.AddDays(-((7 + (int)x.IssueDate.DayOfWeek - 1) % 7))
        }).OrderBy(x => x.Key.WeekStart).Select(x => new
        {
            x.Key.BranchId, x.Key.BranchCode, x.Key.Currency, x.Key.WeekStart,
            InvoiceCount = x.Count(), Sales = x.Sum(y => y.TotalAmount),
            Collected = x.Sum(y => y.AmountPaid)
        }));
    }

    [HttpGet("operations")]
    [HttpGet("/api/dashboard/overview/shipments")]
    public async Task<ActionResult> Operations(CancellationToken cancellationToken)
    {
        var scope = await _branches.ResolveAsync(false, cancellationToken);
        var shipments = await _context.CustomerShipments.AsNoTracking().Where(x =>
                scope.IsAllBranches || x.BranchId == scope.BranchId)
            .GroupBy(x => new { x.BranchId, x.Method, x.MovementState })
            .Select(x => new { x.Key.BranchId, x.Key.Method, x.Key.MovementState, Count = x.Count() })
            .ToListAsync(cancellationToken);
        var manifests = await _context.OperationalManifests.AsNoTracking().Where(x =>
                scope.IsAllBranches || x.BranchId == scope.BranchId)
            .GroupBy(x => new { x.BranchId, x.LogisticsMethod })
            .Select(x => new { x.Key.BranchId, x.Key.LogisticsMethod, Count = x.Count() })
            .ToListAsync(cancellationToken);
        return Ok(new { Shipments = shipments, Manifests = manifests });
    }

    [HttpGet("alerts")]
    [HttpGet("/api/dashboard/overview/manifest-warnings")]
    public async Task<ActionResult> Alerts(CancellationToken cancellationToken)
    {
        var scope = await _branches.ResolveAsync(false, cancellationToken);
        var unassigned = await _context.CustomerShipments.AsNoTracking().CountAsync(x =>
            (scope.IsAllBranches || x.BranchId == scope.BranchId) &&
            x.MovementState != CustomerShipmentMovementState.Delivered &&
            x.MovementState != CustomerShipmentMovementState.Cancelled &&
            !_context.OperationalManifestEntries.Any(line => line.InvoiceId == x.InvoiceId),
            cancellationToken);
        var overdue = await _context.Invoices.AsNoTracking().CountAsync(x =>
            (scope.IsAllBranches || x.BranchId == scope.BranchId) &&
            x.BalanceDue > 0 && x.IssueDate < DateTime.UtcNow.AddDays(-7) &&
            x.Status != InvoiceStatus.Void, cancellationToken);
        var urgent = await _context.StaffNotifications.AsNoTracking().CountAsync(x =>
            x.UserAccountId == CurrentUserId && x.ReadAt == null && x.IsUrgent &&
            (x.BranchId == null || scope.IsAllBranches || x.BranchId == scope.BranchId),
            cancellationToken);
        return Ok(new { UnassignedShipments = unassigned, OutstandingOverSevenDays = overdue,
            UrgentNotifications = urgent });
    }

    [Authorize(Policy = "FinanceTeam")]
    [HttpGet("/api/dashboard/overview/finance")]
    public async Task<ActionResult> Finance([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var scope = await _branches.ResolveAsync(false, cancellationToken);
        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        if (start >= end || end - start > TimeSpan.FromDays(366))
            return BadRequest("Select a valid period of no more than one year.");
        var rows = await _context.Invoices.AsNoTracking().Where(x =>
                (scope.IsAllBranches || x.BranchId == scope.BranchId) &&
                x.IssueDate >= start && x.IssueDate <= end && x.Status != InvoiceStatus.Void)
            .GroupBy(x => new { x.BranchId, x.IssuerBranchCode, x.Currency })
            .Select(x => new
            {
                x.Key.BranchId, BranchCode = x.Key.IssuerBranchCode, x.Key.Currency,
                InvoiceCount = x.Count(), Invoiced = x.Sum(y => y.TotalAmount),
                Collected = x.Sum(y => y.AmountPaid),
                CreditApplied = x.Sum(y => y.AppliedCreditAmount),
                Outstanding = x.Sum(y => y.BalanceDue)
            }).OrderBy(x => x.BranchCode).ThenBy(x => x.Currency).ToListAsync(cancellationToken);
        return Ok(new { From = start, To = end, Branches = rows });
    }
}
