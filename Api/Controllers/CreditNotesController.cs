using System.Data;
using Api.Exceptions;
using Api.Services;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>Controlled customer credit. This API never pays cash to a customer.</summary>
[Authorize(Policy = "FinanceApprover")]
[Route("api/finance")]
public sealed class CreditNotesController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    private readonly ICompanyDocumentNumberGenerator _numbers;
    private readonly IAccountingService _accounting;

    public CreditNotesController(ApplicationDbContext context, IActiveBranchContext branches,
        ICompanyDocumentNumberGenerator numbers, IAccountingService accounting)
    {
        _context = context;
        _branches = branches;
        _numbers = numbers;
        _accounting = accounting;
    }

    [HttpGet("credit-notes")]
    public async Task<ActionResult> List([FromQuery] Guid? clientId, [FromQuery] CreditNoteState? state,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.CreditNotes.AsNoTracking().Where(x =>
            branch.IsAllBranches || x.BranchId == branch.BranchId);
        if (clientId.HasValue) query = query.Where(x => x.CustomerId == clientId);
        if (state.HasValue) query = query.Where(x => x.State == state);
        if (from.HasValue) query = query.Where(x => x.IssuedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.IssuedAt < to.Value.ToUniversalTime().AddDays(1));
        var result = await query.OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, true, cancellationToken);
        var responses = await MapAsync(result.Items, cancellationToken);
        return Ok(new PagedResponse<CreditNoteResponse>(responses, result.Page, result.PageSize,
            result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
    }

    [HttpPost("credit-notes")]
    public async Task<ActionResult<CreditNoteResponse>> Create(CreateCreditNoteRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var key = RequireIdempotencyKey();
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("CREDIT_NOTE_DETAILS_REQUIRED", "A positive amount and approval reason are required.");
        var strategy = _context.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await LockCommandAsync("credit-note.issue", key, cancellationToken);
            var prior = await FindIdempotencyAsync("credit-note.issue", key, cancellationToken);
            if (prior is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new CommandResult(prior.ResourceId, false);
            }
            var client = await _context.Customers.FirstOrDefaultAsync(x => x.Id == request.ClientId &&
                x.BranchId == branch.BranchId, cancellationToken)
                ?? throw new ApiProblemException(404, "CLIENT_NOT_FOUND", "Client not found",
                    "The client is not available in the active branch.");
            Invoice? original = null;
            if (!string.IsNullOrWhiteSpace(request.OriginalInvoiceNumber))
            {
                var number = request.OriginalInvoiceNumber.Trim().ToUpperInvariant();
                original = await _context.Invoices.FirstOrDefaultAsync(x => x.InvoiceNumber == number &&
                    x.CustomerId == client.Id && x.BranchId == branch.BranchId &&
                    x.Type == InvoiceType.Sales, cancellationToken)
                    ?? throw new ApiProblemException(404, "ORIGINAL_INVOICE_NOT_FOUND",
                        "Original invoice not found", "The invoice is not a compatible client sales invoice.");
            }
            var credit = new CreditNote
            {
                BranchId = branch.BranchId!.Value, CustomerId = client.Id,
                OriginalInvoiceId = original?.Id,
                CreditNoteNumber = await _numbers.NextCreditNoteAsync(cancellationToken),
                Currency = branch.Currency!, OriginalAmount = Money(request.Amount),
                Reason = request.Reason.Trim(), ApprovedByUserId = CurrentUserId, IssuedAt = DateTime.UtcNow
            };
            _context.CreditNotes.Add(credit);
            await _accounting.PrepareJournalAsync("CreditNoteIssue", credit.Id,
                $"Issue customer credit {credit.CreditNoteNumber}", credit.IssuedAt, credit.Currency,
                [new JournalPostingLine("4000", credit.OriginalAmount, 0, credit.Reason),
                 new JournalPostingLine("2100", 0, credit.OriginalAmount, credit.CreditNoteNumber)],
                cancellationToken);
            AddIdempotency("credit-note.issue", key, nameof(CreditNote), credit.Id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CommandResult(credit.Id, true);
        });
        var response = (await MapAsync(await _context.CreditNotes.AsNoTracking()
            .Where(x => x.Id == result.ResourceId).ToListAsync(cancellationToken), cancellationToken)).Single();
        Response.Headers.ETag = Quote(response.RowVersion);
        return result.Created ? CreatedAtAction(nameof(Get), new { creditNoteId = response.Id }, response) : Ok(response);
    }

    [HttpGet("credit-notes/{creditNoteId:guid}")]
    public async Task<ActionResult<CreditNoteResponse>> Get(Guid creditNoteId,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var credit = await _context.CreditNotes.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == creditNoteId && (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken)
            ?? throw new KeyNotFoundException("Credit note not found.");
        var response = (await MapAsync([credit], cancellationToken)).Single();
        Response.Headers.ETag = Quote(response.RowVersion);
        return Ok(response);
    }

    [HttpGet("clients/{clientId:guid}/available-credit")]
    public async Task<ActionResult<AvailableClientCreditResponse>> Available(Guid clientId,
        [FromQuery] string currency, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var code = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || code.Length != 3)
            throw Rule("CURRENCY_REQUIRED", "A three-letter currency is required.");
        var credits = await _context.CreditNotes.AsNoTracking().Where(x => x.CustomerId == clientId &&
            x.Currency == code && x.State != CreditNoteState.Voided && x.State != CreditNoteState.CreditUsed &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId))
            .OrderBy(x => x.IssuedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var responses = await MapAsync(credits, cancellationToken);
        return Ok(new AvailableClientCreditResponse(clientId, code,
            responses.Sum(x => x.AvailableAmount), responses));
    }

    [HttpPost("invoices/{invoiceNumber}/apply-credit")]
    public async Task<ActionResult<InvoiceCreditApplicationResponse>> Apply(string invoiceNumber,
        ApplyInvoiceCreditRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var key = RequireIdempotencyKey();
        if (request.Credits is null || request.Credits.Count == 0 || request.Credits.Any(x => x.Amount <= 0) ||
            request.Credits.Select(x => x.CreditNoteId).Distinct().Count() != request.Credits.Count)
            throw Rule("CREDIT_SELECTION_REQUIRED", "Select one or more unique credits with positive amounts.");
        var normalized = invoiceNumber.Trim().ToUpperInvariant();
        var strategy = _context.Database.CreateExecutionStrategy();
        var operationId = await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await LockCommandAsync("invoice.apply-credit", key, cancellationToken);
            var prior = await FindIdempotencyAsync("invoice.apply-credit", key, cancellationToken);
            if (prior is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return prior.ResourceId;
            }
            var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.InvoiceNumber == normalized &&
                x.BranchId == branch.BranchId && x.Type == InvoiceType.Sales, cancellationToken)
                ?? throw new KeyNotFoundException("Sales invoice not found.");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM invoices WHERE \"Id\" = {invoice.Id} FOR UPDATE", cancellationToken);
            var total = Money(request.Credits.Sum(x => x.Amount));
            if (total > invoice.BalanceDue)
                throw Rule("CREDIT_EXCEEDS_BALANCE", "Selected credit exceeds the invoice balance.");
            var applications = await ApplyCreditsAsync(invoice, request.Credits, branch.BranchId!.Value,
                key, cancellationToken);
            invoice.AppliedCreditAmount += total;
            invoice.RowVersion++;
            SyncInvoiceState(invoice);
            var id = Guid.NewGuid();
            await _accounting.PrepareJournalAsync("CreditApplication", id,
                $"Apply customer credit to {invoice.InvoiceNumber}", DateTime.UtcNow, invoice.Currency,
                [new JournalPostingLine("2100", total, 0, invoice.InvoiceNumber),
                 new JournalPostingLine("1100", 0, total, invoice.InvoiceNumber)], cancellationToken);
            AddIdempotency("invoice.apply-credit", key, "CreditApplicationBatch", id);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return id;
        });
        var record = await _context.IdempotencyRecords.AsNoTracking().FirstAsync(x => x.ActorUserId == CurrentUserId &&
            x.Operation == "invoice.apply-credit" && x.Key == key, cancellationToken);
        var invoiceResult = await _context.Invoices.AsNoTracking().SingleAsync(x => x.InvoiceNumber == normalized,
            cancellationToken);
        var applied = await (from application in _context.CreditApplications.AsNoTracking()
            join credit in _context.CreditNotes.AsNoTracking() on application.CreditNoteId equals credit.Id
            where application.AppliedByUserId == CurrentUserId && application.IdempotencyKey == key
            select new CreditApplicationResponse(application.Id, credit.Id, credit.CreditNoteNumber,
                application.Amount, application.AppliedAt)).ToListAsync(cancellationToken);
        return Ok(new InvoiceCreditApplicationResponse(invoiceResult.InvoiceNumber, applied.Sum(x => x.Amount),
            invoiceResult.AppliedCreditAmount, invoiceResult.AmountPaid, invoiceResult.BalanceDue,
            invoiceResult.PaymentStatus, applied));
    }

    [HttpPost("credit-notes/{creditNoteId:guid}/void")]
    public async Task<IActionResult> Void(Guid creditNoteId, VoidCreditNoteRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var version = RequireVersion();
        if (string.IsNullOrWhiteSpace(request.Reason)) throw Rule("VOID_REASON_REQUIRED", "A void reason is required.");
        var credit = await _context.CreditNotes.FirstOrDefaultAsync(x => x.Id == creditNoteId &&
            x.BranchId == branch.BranchId, cancellationToken) ?? throw new KeyNotFoundException("Credit note not found.");
        if (credit.RowVersion != version) throw new ApiProblemException(412, "VERSION_MISMATCH",
            "The credit note changed", "Reload before attempting the void again.");
        if (credit.State == CreditNoteState.Voided) return NoContent();
        if (credit.UsedAmount > 0) throw Rule("USED_CREDIT_CANNOT_BE_VOIDED",
            "A credit note that has been used cannot be voided.");
        credit.State = CreditNoteState.Voided;
        credit.VoidedAt = DateTime.UtcNow;
        credit.VoidedByUserId = CurrentUserId;
        credit.VoidReason = request.Reason.Trim();
        credit.RowVersion++;
        await _accounting.PrepareJournalAsync("CreditNoteVoid", credit.Id,
            $"Void customer credit {credit.CreditNoteNumber}", credit.VoidedAt.Value, credit.Currency,
            [new JournalPostingLine("2100", credit.OriginalAmount, 0, credit.VoidReason),
             new JournalPostingLine("4000", 0, credit.OriginalAmount, credit.CreditNoteNumber)], cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    internal async Task<IReadOnlyList<CreditApplication>> ApplyCreditsAsync(Invoice invoice,
        IReadOnlyList<ApplyCreditSelection> requested, Guid branchId, Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var ids = requested.Select(x => x.CreditNoteId).OrderBy(x => x).ToList();
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM credit_notes WHERE \"Id\" = ANY({ids.ToArray()}) ORDER BY \"Id\" FOR UPDATE",
            cancellationToken);
        var credits = await _context.CreditNotes.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id,
            cancellationToken);
        var now = DateTime.UtcNow;
        var applications = new List<CreditApplication>();
        foreach (var selected in requested)
        {
            if (!credits.TryGetValue(selected.CreditNoteId, out var credit) || credit.BranchId != branchId ||
                credit.CustomerId != invoice.CustomerId || credit.Currency != invoice.Currency ||
                credit.State is CreditNoteState.Voided or CreditNoteState.CreditUsed)
                throw Rule("INCOMPATIBLE_CREDIT", "The selected credit is unavailable for this client, branch or currency.");
            var amount = Money(selected.Amount);
            if (amount <= 0 || amount > credit.AvailableAmount)
                throw Rule("CREDIT_AMOUNT_UNAVAILABLE", "A selected amount exceeds its available credit.");
            credit.UsedAmount += amount;
            credit.State = credit.AvailableAmount == 0 ? CreditNoteState.CreditUsed : CreditNoteState.CreditPartUsed;
            credit.RowVersion++;
            applications.Add(new CreditApplication
            {
                BranchId = branchId, CreditNoteId = credit.Id, InvoiceId = invoice.Id, Amount = amount,
                AppliedAt = now, AppliedByUserId = CurrentUserId, IdempotencyKey = idempotencyKey
            });
        }
        _context.CreditApplications.AddRange(applications);
        return applications;
    }

    private async Task<IReadOnlyList<CreditNoteResponse>> MapAsync(IReadOnlyCollection<CreditNote> credits,
        CancellationToken cancellationToken)
    {
        if (credits.Count == 0) return [];
        var customerIds = credits.Select(x => x.CustomerId).Distinct().ToList();
        var invoiceIds = credits.Where(x => x.OriginalInvoiceId.HasValue).Select(x => x.OriginalInvoiceId!.Value)
            .Distinct().ToList();
        var customers = await _context.Customers.AsNoTracking().Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var invoices = await _context.Invoices.AsNoTracking().Where(x => invoiceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return credits.Select(x => new CreditNoteResponse(x.Id, x.CreditNoteNumber, x.RowVersion, x.BranchId,
            x.CustomerId, customers[x.CustomerId].ClientNumber, customers[x.CustomerId].FullName,
            x.OriginalInvoiceId, x.OriginalInvoiceId.HasValue && invoices.TryGetValue(x.OriginalInvoiceId.Value, out var inv)
                ? inv.InvoiceNumber : null, x.Currency, x.OriginalAmount, x.UsedAmount, x.AvailableAmount,
            x.State, x.Reason, x.ApprovedByUserId, x.IssuedAt, x.VoidedAt, x.VoidReason)).ToList();
    }

    private Guid RequireIdempotencyKey() => Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key)
        ? key : throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency key required",
            "Send a UUID Idempotency-Key header.");
    private long RequireVersion()
    {
        var raw = Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        return long.TryParse(raw, out var version) ? version : throw new ApiProblemException(428,
            "IF_MATCH_REQUIRED", "Version required", "Send the current row version in If-Match.");
    }
    private async Task LockCommandAsync(string operation, Guid key, CancellationToken cancellationToken)
    {
        var value = $"{CurrentUserId:N}:{operation}:{key:N}";
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({value}))", cancellationToken);
    }
    private Task<IdempotencyRecord?> FindIdempotencyAsync(string operation, Guid key,
        CancellationToken cancellationToken) => _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
        x.ActorUserId == CurrentUserId && x.Operation == operation && x.Key == key && x.ExpiresAt > DateTime.UtcNow,
        cancellationToken);
    private void AddIdempotency(string operation, Guid key, string resourceType, Guid resourceId) =>
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId, Operation = operation,
            Key = key, ResourceType = resourceType, ResourceId = resourceId, ExpiresAt = DateTime.UtcNow.AddHours(24) });
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Quote(long version) => $"\"{version}\"";
    private static ApiProblemException Rule(string code, string message) =>
        new(422, code, "Credit operation cannot be completed", message);
    private static void SyncInvoiceState(Invoice invoice)
    {
        if (invoice.BalanceDue == 0) { invoice.PaymentStatus = InvoicePaymentStatus.Paid; invoice.Status = InvoiceStatus.Paid; }
        else if (invoice.AmountPaid > 0 || invoice.AppliedCreditAmount > 0)
        { invoice.PaymentStatus = InvoicePaymentStatus.PartiallyPaid; invoice.Status = InvoiceStatus.PartiallyPaid; }
        else { invoice.PaymentStatus = InvoicePaymentStatus.Unpaid; invoice.Status = InvoiceStatus.Issued; }
    }
    private sealed record CommandResult(Guid ResourceId, bool Created);
}
