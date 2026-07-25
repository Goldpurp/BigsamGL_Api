using Api.Services;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>Compact, active-branch overview for the management dashboard.</summary>
[Authorize(Policy = "StaffOnly")]
[EnableRateLimiting("Reports")]
[RequestTimeout("Reports")]
[Route("api/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;

    public DashboardController(IApplicationDbContext context, IActiveBranchContext branches)
    {
        _context = context;
        _branches = branches;
    }

    [HttpGet("overview")]
    public async Task<ActionResult> Overview([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var periodTo = (to ?? DateTime.UtcNow).ToUniversalTime();
        var periodFrom = (from ?? periodTo.AddDays(-30)).ToUniversalTime();
        if (periodFrom >= periodTo || periodTo - periodFrom > TimeSpan.FromDays(366))
            return BadRequest("Select a valid period of no more than one year.");

        var invoiceQuery = _context.Invoices.AsNoTracking().Where(x =>
            (branch.IsAllBranches || x.BranchId == branch.BranchId) &&
            x.IssueDate >= periodFrom && x.IssueDate <= periodTo && x.Status != InvoiceStatus.Void);
        var finance = await invoiceQuery.GroupBy(x => new { x.BranchId, x.IssuerBranchCode, x.Currency })
            .Select(x => new
            {
                x.Key.BranchId, BranchCode = x.Key.IssuerBranchCode, x.Key.Currency,
                Invoices = x.Count(), Invoiced = x.Sum(y => y.TotalAmount),
                Collected = x.Sum(y => y.AmountPaid), Outstanding = x.Sum(y => y.BalanceDue)
            }).OrderBy(x => x.BranchCode).ThenBy(x => x.Currency).ToListAsync(cancellationToken);

        var shipmentQuery = _context.CustomerShipments.AsNoTracking().Where(x =>
            branch.IsAllBranches || x.BranchId == branch.BranchId);
        var activeShipments = await shipmentQuery.CountAsync(x =>
            x.MovementState != CustomerShipmentMovementState.Delivered &&
            x.MovementState != CustomerShipmentMovementState.Cancelled, cancellationToken);
        var deliveredShipments = await shipmentQuery.CountAsync(x =>
            x.MovementState == CustomerShipmentMovementState.Delivered &&
            x.LastModifiedAt >= periodFrom && x.LastModifiedAt <= periodTo, cancellationToken);
        var clients = await _context.Customers.AsNoTracking().CountAsync(x =>
            x.MergedIntoCustomerId == null && (branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken);
        var openPickups = await _context.ScheduledPickups.AsNoTracking().CountAsync(x =>
            x.DeletedAt == null && (branch.IsAllBranches || x.BranchId == branch.BranchId) &&
            x.State != PickupState.Collected && x.State != PickupState.Cancelled, cancellationToken);
        var unassigned = await shipmentQuery.CountAsync(x =>
            x.MovementState != CustomerShipmentMovementState.Delivered &&
            x.MovementState != CustomerShipmentMovementState.Cancelled &&
            !_context.OperationalManifestEntries.Any(line => line.CustomerShipmentId == x.Id),
            cancellationToken);
        var unread = await _context.StaffNotifications.AsNoTracking().CountAsync(x =>
            x.UserAccountId == CurrentUserId && x.ReadAt == null &&
            (x.BranchId == null || branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken);

        var recentShipments = await (from shipment in shipmentQuery
            join invoice in _context.Invoices.AsNoTracking() on shipment.InvoiceId equals invoice.Id
            orderby shipment.CreatedAt descending
            select new
            {
                shipment.CreatedAt, Type = "Shipment", Reference = shipment.InvoiceNumber,
                Description = shipment.Description, invoice.Currency, Amount = (decimal?)invoice.TotalAmount
            }).Take(6).ToListAsync(cancellationToken);
        var recentTransactions = await _context.FinancialTransactions.AsNoTracking().Where(x =>
                branch.IsAllBranches || x.BranchId == branch.BranchId)
            .OrderByDescending(x => x.OccurredAt).Take(6)
            .Select(x => new
            {
                CreatedAt = x.OccurredAt, Type = "Transaction", Reference = x.TransactionNumber,
                Description = x.CounterpartyName, x.Currency, Amount = (decimal?)x.Amount
            }).ToListAsync(cancellationToken);

        return Ok(new
        {
            From = periodFrom, To = periodTo, GeneratedAt = DateTime.UtcNow,
            Branch = branch.IsAllBranches ? "all" : branch.BranchKey,
            Clients = clients, ActiveShipments = activeShipments, DeliveredShipments = deliveredShipments,
            OpenPickups = openPickups, ManifestWarnings = unassigned, UnreadNotifications = unread,
            FinanceByBranchAndCurrency = finance,
            RecentActivity = recentShipments.Concat(recentTransactions)
                .OrderByDescending(x => x.CreatedAt).Take(8)
        });
    }
}
