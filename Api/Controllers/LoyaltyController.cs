using Application.Common;
using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("api/loyalty")]
public sealed class LoyaltyController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    public LoyaltyController(IApplicationDbContext context) => _context = context;

    [HttpGet("customers/{customerId:guid}")]
    public async Task<ActionResult> CustomerHistory(Guid customerId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == customerId, cancellationToken) ?? throw new KeyNotFoundException("Customer not found.");
        var account = await _context.CustomerLoyaltyAccounts.AsNoTracking().FirstOrDefaultAsync(x =>
            x.CustomerId == customerId, cancellationToken);
        var query = from transaction in _context.LoyaltyPointTransactions.AsNoTracking()
                    join referred in _context.Customers.AsNoTracking()
                        on transaction.ReferredCustomerId equals referred.Id into referredCustomers
                    from referred in referredCustomers.DefaultIfEmpty()
                    join invoice in _context.Invoices.AsNoTracking()
                        on transaction.InvoiceId equals invoice.Id into invoices
                    from invoice in invoices.DefaultIfEmpty()
                    where transaction.CustomerId == customerId
                    orderby transaction.OccurredAt descending, transaction.Id descending
                    select new LoyaltyTransactionResponse(transaction.Id, transaction.Type,
                        transaction.Points, transaction.ReferredCustomerId,
                        referred == null ? null : referred.FullName, transaction.InvoiceId,
                        invoice == null ? null : invoice.InvoiceNumber, transaction.Description,
                        transaction.OccurredAt);
        var history = await query.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
        return Ok(new
        {
            Account = new LoyaltyAccountResponse(customer.Id, customer.FullName,
                account?.AvailablePoints ?? 0, account?.LifetimeEarnedPoints ?? 0),
            History = history
        });
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult> Leaderboard([FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var query = from account in _context.CustomerLoyaltyAccounts.AsNoTracking()
                    join customer in _context.Customers.AsNoTracking() on account.CustomerId equals customer.Id
                    orderby account.LifetimeEarnedPoints descending, customer.FullName, account.CustomerId
                    select new LoyaltyAccountResponse(customer.Id, customer.FullName,
                        account.AvailablePoints, account.LifetimeEarnedPoints);
        return Ok(await query.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }
}
