using Api.Services;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Dashboard;

[ApiController]
[Authorize(Policy = "StaffOnly")]
[EnableRateLimiting("Reports")]
[RequestTimeout("Reports")]
[Route("api/dashboard/sales-performance")]
public sealed class SalesPerformanceController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;

    public SalesPerformanceController(IApplicationDbContext context, IActiveBranchContext branches)
    {
        _context = context;
        _branches = branches;
    }

    [HttpGet]
    public async Task<ActionResult> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var periodTo = (to ?? DateTime.UtcNow).ToUniversalTime();
        var periodFrom = (from ?? periodTo.AddDays(-84)).ToUniversalTime();
        if (periodFrom >= periodTo || periodTo - periodFrom > TimeSpan.FromDays(731))
            return BadRequest("Select a valid sales period of no more than two years.");

        var invoices = await _context.Invoices.AsNoTracking().Where(x =>
                (branch.IsAllBranches || x.BranchId == branch.BranchId) &&
                x.Status != InvoiceStatus.Void && x.IssueDate >= periodFrom && x.IssueDate <= periodTo)
            .Select(x => new
            {
                x.BranchId, x.IssuerBranchCode, x.Currency, x.IssueDate, x.Type,
                x.TotalAmount, x.AmountPaid, x.AppliedCreditAmount
            }).ToListAsync(cancellationToken);

        var periods = invoices.GroupBy(x => new
            {
                x.BranchId, BranchCode = x.IssuerBranchCode, x.Currency,
                WeekStart = StartOfWeek(x.IssueDate)
            }).OrderBy(x => x.Key.WeekStart).ThenBy(x => x.Key.BranchCode)
            .Select(x => new
            {
                x.Key.BranchId, x.Key.BranchCode, x.Key.Currency, x.Key.WeekStart,
                WeekEnd = x.Key.WeekStart.AddDays(7).AddTicks(-1),
                SalesInvoices = x.Count(y => y.Type == InvoiceType.Sales),
                CreditNotes = x.Count(y => y.Type == InvoiceType.CreditNote),
                Invoiced = x.Where(y => y.Type == InvoiceType.Sales).Sum(y => y.TotalAmount),
                Collected = x.Where(y => y.Type == InvoiceType.Sales).Sum(y => y.AmountPaid),
                CreditApplied = x.Where(y => y.Type == InvoiceType.Sales).Sum(y => y.AppliedCreditAmount),
                Outstanding = x.Where(y => y.Type == InvoiceType.Sales)
                    .Sum(y => Math.Max(0, y.TotalAmount - y.AmountPaid - y.AppliedCreditAmount))
            }).ToList();

        return Ok(new
        {
            From = periodFrom, To = periodTo,
            Branch = branch.IsAllBranches ? "all" : branch.BranchKey,
            Periods = periods
        });
    }

    private static DateTime StartOfWeek(DateTime value)
    {
        var date = value.Date;
        var days = ((int)date.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(date.AddDays(-days), DateTimeKind.Utc);
    }
}
