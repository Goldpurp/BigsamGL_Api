using Application.Common.Interfaces;
using Api.Services;
using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "FinanceTeam")]
[Route("api/accounting")]
public sealed class AccountingController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IAccountingService _accounting;
    private readonly IActiveBranchContext _branches;
    public AccountingController(IApplicationDbContext context, IAccountingService accounting,
        IActiveBranchContext branches)
    {
        _context = context; _accounting = accounting; _branches = branches;
    }

    [HttpGet("accounts")]
    public async Task<ActionResult> GetAccounts(CancellationToken cancellationToken) =>
        Ok(await _context.LedgerAccounts.AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken));

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("journals")]
    public async Task<ActionResult<JournalEntry>> CreateJournal(CreateJournalRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        if (!string.Equals(request.Currency, branch.Currency, StringComparison.OrdinalIgnoreCase))
            return BadRequest($"The active branch posts journals in {branch.Currency}.");
        var journal = await _accounting.PrepareJournalAsync("Manual", null, request.Description, request.EntryDate,
            request.Currency, request.Lines.Select(x => new JournalPostingLine(x.AccountCode, x.Debit, x.Credit, x.Description)).ToList(), cancellationToken,
            branch.BranchId);
        await _context.SaveChangesAsync(cancellationToken); return Created($"/api/accounting/journals/{journal.Id}", journal);
    }

    [HttpGet("journals")]
    public async Task<ActionResult> GetJournals([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] bool paged = false, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var start = from ?? DateTime.UtcNow.AddDays(-30); var end = to ?? DateTime.UtcNow;
        var ordered = _context.JournalEntries.AsNoTracking().Where(x =>
                x.EntryDate >= start && x.EntryDate <= end &&
                (branch.IsAllBranches || x.BranchId == branch.BranchId))
            .OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.Id);
        PagedResponse<JournalEntry>? pageResult = null;
        IReadOnlyList<JournalEntry> entries;
        if (paged)
        {
            pageResult = await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
            entries = pageResult.Items;
        }
        else entries = await ordered.Take(200).ToListAsync(cancellationToken);
        var ids = entries.Select(x => x.Id).ToList(); var lines = await _context.JournalLines.AsNoTracking().Where(x => ids.Contains(x.JournalEntryId)).ToListAsync(cancellationToken);
        var items = entries.Select(x => new { Entry = x,
            Lines = lines.Where(l => l.JournalEntryId == x.Id).ToList() }).ToList();
        return pageResult is null ? Ok(items) : Ok(new { items, pageResult.Page, pageResult.PageSize,
            pageResult.HasPrevious, pageResult.HasNext, pageResult.Total, pageResult.TotalPages });
    }

    [HttpGet("trial-balance")]
    [EnableRateLimiting("Reports")]
    [RequestTimeout("Reports")]
    public async Task<ActionResult> TrialBalance([FromQuery] DateTime? to, [FromQuery] string currency = "GBP", CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var end = to ?? DateTime.UtcNow; currency = currency.Trim().ToUpperInvariant();
        if (!branch.IsAllBranches && !string.Equals(currency, branch.Currency, StringComparison.Ordinal))
            return BadRequest($"The active branch uses {branch.Currency}.");
        var balances = await (from line in _context.JournalLines.AsNoTracking()
            join entry in _context.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where entry.Status == JournalStatus.Posted && entry.EntryDate <= end &&
                  line.Currency == currency &&
                  (branch.IsAllBranches || entry.BranchId == branch.BranchId)
            group line by line.LedgerAccountId into accountLines
            select new { LedgerAccountId = accountLines.Key, Debit = accountLines.Sum(x => x.Debit),
                Credit = accountLines.Sum(x => x.Credit) }).ToDictionaryAsync(x => x.LedgerAccountId,
                cancellationToken);
        var accounts = await _context.LedgerAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(cancellationToken);
        var result = accounts.Select(account =>
        {
            balances.TryGetValue(account.Id, out var balance);
            return new { account.Code, account.Name, account.Type, Currency = currency,
                Debit = balance?.Debit ?? 0, Credit = balance?.Credit ?? 0,
                Balance = (balance?.Debit ?? 0) - (balance?.Credit ?? 0) };
        }).ToList();
        return Ok(new { AsAt = end, Currency = currency, Accounts = result,
            TotalDebits = result.Sum(x => x.Debit), TotalCredits = result.Sum(x => x.Credit) });
    }

    [HttpGet("accounts/{code}/entries")]
    public async Task<ActionResult> AccountEntries(string code, [FromQuery] DateTime? from,
        [FromQuery] DateTime? to, [FromQuery] string? currency,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var accountCode = code.Trim().ToUpperInvariant();
        var account = await _context.LedgerAccounts.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Code == accountCode, cancellationToken);
        if (account is null) return NotFound();
        var query = from line in _context.JournalLines.AsNoTracking()
                    join entry in _context.JournalEntries.AsNoTracking()
                        on line.JournalEntryId equals entry.Id
                    where line.LedgerAccountId == account.Id &&
                          (branch.IsAllBranches || entry.BranchId == branch.BranchId)
                    select new { Entry = entry, Line = line };
        if (from.HasValue) query = query.Where(x => x.Entry.EntryDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.Entry.EntryDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant();
            query = query.Where(x => x.Line.Currency == value);
        }
        var projected = query.OrderByDescending(x => x.Entry.EntryDate)
            .ThenByDescending(x => x.Line.Id).Select(x => new
            {
                x.Entry.Id, x.Entry.JournalNumber, x.Entry.EntryDate, x.Entry.Description,
                x.Entry.SourceType, x.Entry.Status, x.Line.Currency, x.Line.Debit, x.Line.Credit,
                LineDescription = x.Line.Description
            });
        return Ok(await projected.ToPagedResponseAsync(page, pageSize, true, cancellationToken));
    }

    [HttpGet("journals/{id:guid}")]
    public async Task<ActionResult> Journal(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var entry = await _context.JournalEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken);
        if (entry is null) return NotFound();
        var lines = await (from line in _context.JournalLines.AsNoTracking()
            join account in _context.LedgerAccounts.AsNoTracking()
                on line.LedgerAccountId equals account.Id
            where line.JournalEntryId == id
            orderby line.CreatedAt, line.Id
            select new { account.Code, Account = account.Name, line.Description,
                line.Debit, line.Credit, line.Currency }).ToListAsync(cancellationToken);
        return Ok(new { Entry = entry, Lines = lines,
            TotalDebit = lines.Sum(x => x.Debit), TotalCredit = lines.Sum(x => x.Credit) });
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("journals/adjustments")]
    public async Task<ActionResult<JournalEntry>> Adjustment(CreateJournalRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        if (!string.Equals(request.Currency, branch.Currency, StringComparison.OrdinalIgnoreCase))
            return BadRequest($"The active branch posts adjustments in {branch.Currency}.");
        var journal = await _accounting.PrepareJournalAsync("Adjustment", null,
            request.Description, request.EntryDate, request.Currency,
            request.Lines.Select(x => new JournalPostingLine(x.AccountCode, x.Debit, x.Credit,
                x.Description)).ToList(), cancellationToken, branch.BranchId);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/accounting/journals/{journal.Id}", journal);
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("journals/{id:guid}/reverse")]
    public async Task<ActionResult> ReverseJournal(Guid id, ReverseJournalRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("A reversal reason is required.");
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var original = await _context.JournalEntries.FirstOrDefaultAsync(x => x.Id == id &&
            x.BranchId == branch.BranchId, cancellationToken);
        if (original is null) return NotFound();
        if (original.Status != JournalStatus.Posted) return Conflict("Only a posted journal can be reversed.");
        if (await _context.JournalEntries.AsNoTracking().AnyAsync(x =>
            x.ReversalOfJournalId == original.Id, cancellationToken))
            return Conflict("This journal already has a reversal.");
        var lines = await (from line in _context.JournalLines.AsNoTracking()
            join account in _context.LedgerAccounts.AsNoTracking()
                on line.LedgerAccountId equals account.Id
            where line.JournalEntryId == original.Id
            select new JournalPostingLine(account.Code, line.Credit, line.Debit,
                $"Reverse: {line.Description}")).ToListAsync(cancellationToken);
        var reversal = await _accounting.PrepareJournalAsync("JournalReversal", original.Id,
            $"Reverse {original.JournalNumber}: {request.Reason.Trim()}", DateTime.UtcNow,
            original.Currency, lines, cancellationToken, branch.BranchId);
        reversal.ReversalOfJournalId = original.Id;
        original.Status = JournalStatus.Reversed;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { Original = original, Reversal = reversal });
    }

    [HttpGet("reconciliation")]
    public async Task<ActionResult> Reconciliation([FromQuery] DateTime? from,
        [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var start = from ?? DateTime.UtcNow.AddDays(-30);
        var end = to ?? DateTime.UtcNow;
        var transactions = await _context.FinancialTransactions.AsNoTracking().Where(x =>
                x.OccurredAt >= start && x.OccurredAt <= end &&
                (branch.IsAllBranches || x.BranchId == branch.BranchId))
            .Select(x => new { x.Id, x.TransactionNumber, x.BranchCode, x.Currency, x.Amount,
                x.Direction, x.Status, x.JournalEntryId }).ToListAsync(cancellationToken);
        var journalIds = transactions.Where(x => x.JournalEntryId.HasValue)
            .Select(x => x.JournalEntryId!.Value).ToArray();
        var balanced = await _context.JournalLines.AsNoTracking().Where(x =>
                journalIds.Contains(x.JournalEntryId))
            .GroupBy(x => x.JournalEntryId).Select(x => new
            {
                JournalEntryId = x.Key, Debit = x.Sum(y => y.Debit), Credit = x.Sum(y => y.Credit)
            }).ToDictionaryAsync(x => x.JournalEntryId, cancellationToken);
        var exceptions = transactions.Where(x => !x.JournalEntryId.HasValue ||
            !balanced.TryGetValue(x.JournalEntryId.Value, out var totals) ||
            totals.Debit != totals.Credit).ToList();
        return Ok(new { From = start, To = end, Transactions = transactions.Count,
            Reconciled = transactions.Count - exceptions.Count, Exceptions = exceptions });
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("periods/{period}/close")]
    public async Task<ActionResult> ClosePeriod(string period, AccountingPeriodRequest request,
        CancellationToken cancellationToken) =>
        await SetPeriod(period, request, true, cancellationToken);

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("periods/{period}/reopen")]
    public async Task<ActionResult> ReopenPeriod(string period, AccountingPeriodRequest request,
        CancellationToken cancellationToken) =>
        await SetPeriod(period, request, false, cancellationToken);

    private async Task<ActionResult> SetPeriod(string period, AccountingPeriodRequest request,
        bool closed, CancellationToken cancellationToken)
    {
        if (!DateTime.TryParseExact($"{period}-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var start))
            return BadRequest("Period must use yyyy-MM.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("A close or reopen reason is required.");
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var currency = request.Currency.Trim().ToUpperInvariant();
        if (currency != branch.Currency) return BadRequest($"The active branch uses {branch.Currency}.");
        if (closed && await _context.JournalEntries.AsNoTracking().AnyAsync(x =>
            x.BranchId == branch.BranchId && x.Currency == currency &&
            x.EntryDate >= start && x.EntryDate < start.AddMonths(1) &&
            x.Status == JournalStatus.Draft, cancellationToken))
            return Conflict("Post or remove all draft journals before closing this period.");
        var control = await _context.AccountingPeriods.FirstOrDefaultAsync(x =>
            x.BranchId == branch.BranchId && x.Currency == currency && x.Period == period,
            cancellationToken);
        if (control is null)
        {
            control = new AccountingPeriod
            {
                BranchId = branch.BranchId!.Value, Currency = currency, Period = period
            };
            _context.AccountingPeriods.Add(control);
        }
        control.IsClosed = closed;
        control.Reason = request.Reason.Trim();
        if (closed)
        {
            control.ClosedAt = DateTime.UtcNow;
            control.ClosedByUserId = CurrentUserId;
        }
        else
        {
            control.ReopenedAt = DateTime.UtcNow;
            control.ReopenedByUserId = CurrentUserId;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(control);
    }

    [HttpPost("suppliers")]
    public async Task<ActionResult<Supplier>> CreateSupplier(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = new Supplier { SupplierNumber = $"BGL-SUP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Name = request.Name.Trim(), Email = request.Email.Trim().ToLowerInvariant(), Phone = request.Phone.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(), TaxNumber = request.TaxNumber.Trim() };
        _context.Suppliers.Add(supplier); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/accounting/suppliers/{supplier.Id}", supplier);
    }

    [HttpGet("suppliers")]
    public async Task<ActionResult> GetSuppliers([FromQuery] bool paged = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var ordered = _context.Suppliers.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id);
        if (paged) return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
        return Ok(await ordered.Take(500).ToListAsync(cancellationToken));
    }

    [HttpPost("supplier-bills")]
    public async Task<ActionResult<SupplierBill>> CreateSupplierBill(CreateSupplierBillRequest request, CancellationToken cancellationToken)
    {
        if (request.NetAmount < 0 || request.TaxAmount < 0 || request.DueDate < request.BillDate) return BadRequest("Supplier bill values are invalid.");
        if (!await _context.Suppliers.AnyAsync(x => x.Id == request.SupplierId, cancellationToken)) return BadRequest("Supplier not found.");
        var bill = new SupplierBill { SupplierId = request.SupplierId, BillNumber = $"BGL-BILL-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            SupplierReference = request.SupplierReference.Trim(), BillDate = request.BillDate, DueDate = request.DueDate,
            NetAmount = request.NetAmount, TaxAmount = request.TaxAmount, TotalAmount = request.NetAmount + request.TaxAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(), Status = SupplierBillStatus.Submitted, Description = request.Description.Trim() };
        _context.SupplierBills.Add(bill); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/accounting/supplier-bills/{bill.Id}", bill);
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("supplier-bills/{id:guid}/approve")]
    public async Task<IActionResult> ApproveSupplierBill(Guid id, CancellationToken cancellationToken)
    {
        var bill = await _context.SupplierBills.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Supplier bill not found.");
        if (bill.Status != SupplierBillStatus.Submitted) return Conflict("Only submitted bills can be approved.");
        bill.Status = SupplierBillStatus.Approved;
        await _accounting.PrepareJournalAsync("SupplierBill", bill.Id, $"Supplier bill {bill.BillNumber}", bill.BillDate, bill.Currency,
            new[] { new JournalPostingLine("5000", bill.TotalAmount, 0, bill.Description), new JournalPostingLine("2000", 0, bill.TotalAmount, bill.SupplierReference) }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPost("purchase-orders")]
    public async Task<ActionResult<PurchaseOrder>> CreatePurchaseOrder(CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.TotalAmount < 0 || !await _context.Suppliers.AnyAsync(x => x.Id == request.SupplierId, cancellationToken)) return BadRequest("Purchase order values are invalid.");
        var order = new PurchaseOrder { SupplierId = request.SupplierId, PurchaseOrderNumber = $"BGL-PO-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            OrderDate = request.OrderDate, ExpectedAt = request.ExpectedAt, TotalAmount = request.TotalAmount,
            Currency = request.Currency.Trim().ToUpperInvariant(), Description = request.Description.Trim(), Status = PurchaseOrderStatus.Draft };
        _context.PurchaseOrders.Add(order); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/accounting/purchase-orders/{order.Id}", order);
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPatch("purchase-orders/{id:guid}/status")]
    public async Task<IActionResult> ChangePurchaseOrderStatus(Guid id, ChangePurchaseOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _context.PurchaseOrders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Purchase order not found.");
        order.Status = request.Status; await _context.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("bank-accounts")]
    public async Task<ActionResult<BankAccount>> CreateBankAccount(CreateBankAccountRequest request, CancellationToken cancellationToken)
    {
        var account = new BankAccount { Name = request.Name.Trim(), BankName = request.BankName.Trim(),
            MaskedAccountNumber = request.MaskedAccountNumber.Trim(), Currency = request.Currency.Trim().ToUpperInvariant(), OpeningBalance = request.OpeningBalance };
        _context.BankAccounts.Add(account); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/accounting/bank-accounts/{account.Id}", account);
    }
}
