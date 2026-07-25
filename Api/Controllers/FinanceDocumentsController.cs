using System.Data;
using System.Net;
using System.Text;
using Application.Common;
using Application.Common.Interfaces;
using Api.Services;
using Domain.Enums;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "FinanceTeam")]
[Route("api/finance")]
public sealed class FinanceDocumentsController : ApiControllerBase
{
    private const string InflowOperation = "finance.inflow.create";
    private readonly ApplicationDbContext _context;
    private readonly IFinancialPostingService _postings;
    private readonly IAccountingService _accounting;
    private readonly ICompanyDocumentNumberGenerator _numbers;
    private readonly IActiveBranchContext _branches;
    public FinanceDocumentsController(ApplicationDbContext context, IFinancialPostingService postings,
        IAccountingService accounting, ICompanyDocumentNumberGenerator numbers, IActiveBranchContext branches)
    {
        _context = context;
        _postings = postings;
        _accounting = accounting;
        _numbers = numbers;
        _branches = branches;
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("inflows")]
    public async Task<ActionResult> RecordInflow(RecordFinancialInflowRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        if (!Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var idempotencyKey))
            return BadRequest(new { Code = "IDEMPOTENCY_KEY_REQUIRED",
                Message = "Send a UUID Idempotency-Key header." });
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Counterparty) ||
            string.IsNullOrWhiteSpace(request.Reference) || string.IsNullOrWhiteSpace(request.Description))
            return UnprocessableEntity(new { Code = "INFLOW_DETAILS_REQUIRED",
                Message = "Counterparty, positive amount, reference and description are required." });
        if (!string.Equals(request.Currency, branch.Currency, StringComparison.OrdinalIgnoreCase))
            return UnprocessableEntity(new { Code = "BRANCH_CURRENCY_REQUIRED",
                Message = $"The active branch records incoming money in {branch.Currency}." });
        if (!PaymentRouting.IsSupported(request.Method))
            return UnprocessableEntity(new { Code = "PAYMENT_METHOD_NOT_SUPPORTED",
                Message = "Incoming money supports Cash, Card or BankTransfer." });
        if (request.Type is not (FinancialTransactionType.OtherIncome or
            FinancialTransactionType.CapitalIntroduced or FinancialTransactionType.LoanReceived or
            FinancialTransactionType.Adjustment))
            return UnprocessableEntity(new { Code = "INFLOW_TYPE_NOT_SUPPORTED",
                Message = "Choose OtherIncome, CapitalIntroduced, LoanReceived or Adjustment." });
        if (request.OccurredAt == default || request.OccurredAt.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            return UnprocessableEntity(new { Code = "INFLOW_DATE_INVALID",
                Message = "The transaction time is required and cannot be in the future." });

        var existing = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == InflowOperation &&
            x.Key == idempotencyKey && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (existing is not null) return Ok(await InflowResponseAsync(existing.ResourceId, cancellationToken));

        var strategy = _context.Database.CreateExecutionStrategy();
        var transactionId = await strategy.ExecuteAsync(async () =>
        {
            await using var databaseTransaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var lockKey = $"{CurrentUserId:N}:{InflowOperation}:{idempotencyKey:N}";
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);
            var replay = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
                x.ActorUserId == CurrentUserId && x.Operation == InflowOperation &&
                x.Key == idempotencyKey && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
            if (replay is not null)
            {
                await databaseTransaction.CommitAsync(cancellationToken);
                return replay.ResourceId;
            }

            var occurredAt = request.OccurredAt.ToUniversalTime();
            var account = PaymentRouting.AccountCode(request.Method);
            var creditAccount = request.Type switch
            {
                FinancialTransactionType.CapitalIntroduced => "3000",
                FinancialTransactionType.LoanReceived => "2000",
                _ => "4100"
            };
            var financial = new FinancialTransaction
            {
                BranchId = branch.BranchId!.Value,
                TransactionNumber = await _numbers.NextTransactionAsync(cancellationToken),
                Direction = FinancialTransactionDirection.Credit,
                Type = request.Type,
                SourceType = "DirectInflow",
                CounterpartyName = request.Counterparty.Trim(),
                Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
                Currency = branch.Currency!,
                Method = request.Method,
                SettlementAccountCode = account,
                ExternalReference = request.Reference.Trim(),
                OccurredAt = occurredAt,
                BranchCode = branch.Code!,
                RecordedByUserId = CurrentUserId,
                FreightClassification = request.FreightClassification
            };
            financial.SourceId = financial.Id;
            var journal = await _accounting.PrepareJournalAsync(nameof(FinancialTransaction),
                financial.Id, request.Description.Trim(), occurredAt, financial.Currency,
                [
                    new JournalPostingLine(account, financial.Amount, 0, request.Reference.Trim()),
                    new JournalPostingLine(creditAccount, 0, financial.Amount, request.Description.Trim())
                ], cancellationToken, branch.BranchId);
            financial.JournalEntryId = journal.Id;
            var receipt = new TransactionDocument
            {
                DocumentNumber = await _numbers.NextFinancialDocumentAsync(
                    FinancialDocumentType.Receipt, cancellationToken),
                Type = FinancialDocumentType.Receipt,
                FinancialTransactionId = financial.Id,
                IssuedAt = DateTime.UtcNow,
                IssuedByUserId = CurrentUserId,
                VerificationCode = DocumentSecurity.NewVerificationCode()
            };
            _context.FinancialTransactions.Add(financial);
            _context.TransactionDocuments.Add(receipt);
            _context.IdempotencyRecords.Add(new IdempotencyRecord
            {
                ActorUserId = CurrentUserId,
                Operation = InflowOperation,
                Key = idempotencyKey,
                ResourceType = nameof(FinancialTransaction),
                ResourceId = financial.Id,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await _context.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
            return financial.Id;
        });
        return Created($"/api/finance/transactions/{transactionId}",
            await InflowResponseAsync(transactionId, cancellationToken));
    }

    [HttpGet("transactions")]
    public async Task<ActionResult> GetTransactions([FromQuery] FinancialTransactionDirection? direction,
        [FromQuery] FinancialTransactionType? type, [FromQuery] FinancialTransactionStatus? status,
        [FromQuery] PaymentMethod? method, [FromQuery] string? currency, [FromQuery] string? branchCode,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.FinancialTransactions.AsNoTracking().Where(x =>
            activeBranch.IsAllBranches || x.BranchId == activeBranch.BranchId);
        if (direction.HasValue) query = query.Where(x => x.Direction == direction);
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (method.HasValue) query = query.Where(x => x.Method == method);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Currency == value);
        }
        if (activeBranch.IsAllBranches && !string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant(); query = query.Where(x => x.BranchCode == value);
        }
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.TransactionNumber.ToLower().Contains(value) ||
                x.ExternalReference.ToLower().Contains(value) || x.CounterpartyName.ToLower().Contains(value));
        }
        var ordered = query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Select(x => new FinancialTransactionListItem(x.Id, x.TransactionNumber, x.Direction, x.Type,
                x.Status, x.SourceType, x.SourceId, x.CounterpartyName, x.Amount, x.Currency, x.Method,
                x.SettlementAccountCode, x.BankAccountId, x.ExternalReference, x.OccurredAt, x.BranchCode,
                x.FreightClassification, x.JournalEntryId));
        return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("receipts")]
    public async Task<ActionResult> GetDocuments([FromQuery] FinancialDocumentType? type,
        [FromQuery] FinancialDocumentStatus? status, [FromQuery] string? currency,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var query = from document in _context.TransactionDocuments.AsNoTracking()
                    join transaction in _context.FinancialTransactions.AsNoTracking()
                        on document.FinancialTransactionId equals transaction.Id
                    where activeBranch.IsAllBranches || transaction.BranchId == activeBranch.BranchId
                    select new { Document = document, Transaction = transaction };
        if (type.HasValue) query = query.Where(x => x.Document.Type == type);
        if (status.HasValue) query = query.Where(x => x.Document.Status == status);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Transaction.Currency == value);
        }
        if (from.HasValue) query = query.Where(x => x.Document.IssuedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.Document.IssuedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Document.DocumentNumber.ToLower().Contains(value) ||
                x.Transaction.TransactionNumber.ToLower().Contains(value) ||
                x.Transaction.CounterpartyName.ToLower().Contains(value) ||
                x.Transaction.ExternalReference.ToLower().Contains(value));
        }
        var ordered = query.OrderByDescending(x => x.Document.IssuedAt).ThenByDescending(x => x.Document.Id)
            .Select(x => new TransactionDocumentListItem(x.Document.Id, x.Document.DocumentNumber,
                x.Document.Type, x.Document.Status,
                x.Document.FinancialTransactionId, x.Transaction.TransactionNumber, x.Document.InvoiceId,
                x.Document.PaymentId, x.Document.CompanyPurchaseId, x.Document.PurchasePayoutId,
                x.Transaction.CounterpartyName, x.Transaction.Amount, x.Transaction.Currency,
                x.Transaction.Method, x.Document.IssuedAt, x.Document.VerificationCode));
        return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("receipts/{id:guid}")]
    public async Task<ActionResult<TransactionDocumentListItem>> GetDocument(Guid id,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var value = await (from document in _context.TransactionDocuments.AsNoTracking()
            join transaction in _context.FinancialTransactions.AsNoTracking()
                on document.FinancialTransactionId equals transaction.Id
            where document.Id == id && (branch.IsAllBranches || transaction.BranchId == branch.BranchId)
            select new TransactionDocumentListItem(document.Id, document.DocumentNumber, document.Type,
                document.Status, document.FinancialTransactionId, transaction.TransactionNumber,
                document.InvoiceId, document.PaymentId, document.CompanyPurchaseId, document.PurchasePayoutId,
                transaction.CounterpartyName, transaction.Amount, transaction.Currency, transaction.Method,
                document.IssuedAt, document.VerificationCode)).FirstOrDefaultAsync(cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpGet("receipts/{receiptNumber:regex(^BGL\\d+RCT$)}")]
    public async Task<ActionResult<TransactionDocumentListItem>> GetDocumentByNumber(string receiptNumber,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var number = receiptNumber.Trim().ToUpperInvariant();
        var value = await (from document in _context.TransactionDocuments.AsNoTracking()
            join transaction in _context.FinancialTransactions.AsNoTracking()
                on document.FinancialTransactionId equals transaction.Id
            where document.DocumentNumber == number &&
                  (branch.IsAllBranches || transaction.BranchId == branch.BranchId)
            select new TransactionDocumentListItem(document.Id, document.DocumentNumber, document.Type,
                document.Status, document.FinancialTransactionId, transaction.TransactionNumber,
                document.InvoiceId, document.PaymentId, document.CompanyPurchaseId,
                document.PurchasePayoutId, transaction.CounterpartyName, transaction.Amount,
                transaction.Currency, transaction.Method, document.IssuedAt,
                document.VerificationCode)).FirstOrDefaultAsync(cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("receipts/{receiptNumber:regex(^BGL\\d+RCT$)}/void")]
    public async Task<ActionResult> VoidReceipt(string receiptNumber, VoidReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var number = receiptNumber.Trim().ToUpperInvariant();
        var belongsToBranch = await (from document in _context.TransactionDocuments.AsNoTracking()
            join transaction in _context.FinancialTransactions.AsNoTracking()
                on document.FinancialTransactionId equals transaction.Id
            where document.DocumentNumber == number && transaction.BranchId == branch.BranchId
            select document.Id).AnyAsync(cancellationToken);
        if (!belongsToBranch) return NotFound();
        var result = await _postings.VoidReceiptAsync(
            new ReceiptVoidCommand(number, request.Reason, CurrentUserId), cancellationToken);
        return Ok(new
        {
            Receipt = result.Receipt,
            ReversalTransaction = result.ReversalTransaction,
            InvoiceNumber = result.Invoice.InvoiceNumber,
            result.Invoice.PaymentStatus,
            result.Invoice.BalanceDue
        });
    }

    [HttpGet("receipts/{id:guid}/print")]
    public async Task<IActionResult> PrintDocument(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var data = await (from document in _context.TransactionDocuments.AsNoTracking()
            join transaction in _context.FinancialTransactions.AsNoTracking()
                on document.FinancialTransactionId equals transaction.Id
            where document.Id == id && (branch.IsAllBranches || transaction.BranchId == branch.BranchId)
            select new { Document = document, Transaction = transaction }).FirstOrDefaultAsync(cancellationToken);
        if (data is null) return NotFound();
        var invoiceNumber = data.Document.InvoiceId.HasValue
            ? await _context.Invoices.AsNoTracking().Where(x => x.Id == data.Document.InvoiceId)
                .Select(x => x.InvoiceNumber).FirstOrDefaultAsync(cancellationToken) : null;
        var purchaseNumber = data.Document.CompanyPurchaseId.HasValue
            ? await _context.CompanyPurchases.AsNoTracking().Where(x => x.Id == data.Document.CompanyPurchaseId)
                .Select(x => x.PurchaseNumber).FirstOrDefaultAsync(cancellationToken) : null;
        var company = await _context.CompanyProfiles.AsNoTracking().OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return Content(BuildTransactionDocumentHtml(company?.TradingName ?? "BiGSAM Global Logistics",
            company?.RegisteredAddress ?? string.Empty, company?.Email ?? string.Empty,
            data.Document.DocumentNumber, data.Document.Type.ToString(), data.Transaction.CounterpartyName,
            data.Transaction.Amount, data.Transaction.Currency, data.Transaction.Method.ToString(),
            data.Transaction.ExternalReference, data.Transaction.TransactionNumber,
            invoiceNumber ?? purchaseNumber ?? string.Empty, data.Document.IssuedAt,
            data.Document.VerificationCode), "text/html; charset=utf-8");
    }

    [HttpGet("invoices/{id:guid}/print")]
    public async Task<IActionResult> PrintInvoice(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken);
        if (invoice is null) return NotFound();
        var lines = await _context.InvoiceLines.AsNoTracking().Where(x => x.InvoiceId == id)
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var company = await _context.CompanyProfiles.AsNoTracking().OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        static string encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
        var rows = string.Join(string.Empty, lines.Select(x => $"<tr><td>{encode(x.Description)}</td>" +
            $"<td class='num'>{x.Quantity:N3}</td><td class='num'>{x.UnitPrice:N2}</td>" +
            $"<td class='num'>{x.TaxRate:P2}</td><td class='num'>{x.LineTotal:N2}</td></tr>"));
        var html = Header(company?.TradingName ?? "BiGSAM Global Logistics", "Invoice") +
            $"<h1>{encode(invoice.Type.ToString())} invoice</h1><div class='grid'><div><b>Invoice:</b> {encode(invoice.InvoiceNumber)}</div>" +
            $"<div><b>Client:</b> {encode(invoice.CustomerName)}</div><div><b>Issued:</b> {invoice.IssueDate:yyyy-MM-dd}</div>" +
            $"<div><b>Due:</b> {invoice.DueDate:yyyy-MM-dd}</div><div><b>Status:</b> {encode(invoice.Status.ToString())}</div>" +
            $"<div><b>Payment:</b> {encode(invoice.PaymentStatus.ToString())} / {encode(invoice.PreferredPaymentMethod?.ToString() ?? "Not applicable")}</div></div>" +
            "<table><thead><tr><th>Description</th><th>Qty</th><th>Unit price</th><th>Tax</th><th>Total</th></tr></thead><tbody>" + rows + "</tbody></table>" +
            $"<div class='totals'><div>Subtotal: {encode(invoice.Currency)} {invoice.Subtotal:N2}</div><div>Tax: {invoice.TaxAmount:N2}</div>" +
            $"<div>Discount: {invoice.DiscountAmount:N2}</div><strong>Total: {encode(invoice.Currency)} {invoice.TotalAmount:N2}</strong>" +
            $"<div>Paid: {invoice.AmountPaid:N2}</div><div>Credits: {invoice.AppliedCreditAmount:N2}</div><strong>Balance: {invoice.BalanceDue:N2}</strong></div>" +
            $"<p class='verify'>Verification code: {encode(invoice.VerificationCode)}</p>" + Footer();
        return Content(html, "text/html; charset=utf-8");
    }

    [AllowAnonymous]
    [EnableRateLimiting("PublicRead")]
    [HttpGet("documents/verify")]
    public async Task<ActionResult> Verify([FromQuery] string documentNumber, [FromQuery] string verificationCode,
        CancellationToken cancellationToken)
    {
        var number = documentNumber.Trim().ToUpperInvariant();
        var code = verificationCode.Trim().ToUpperInvariant();
        var receipt = await (from document in _context.TransactionDocuments.AsNoTracking()
            join transaction in _context.FinancialTransactions.AsNoTracking()
                on document.FinancialTransactionId equals transaction.Id
            where document.DocumentNumber == number && document.VerificationCode == code
            select new { Valid = true, Kind = document.Type.ToString(), Number = document.DocumentNumber,
                Status = document.Status.ToString(), transaction.Amount, transaction.Currency,
                Date = document.IssuedAt }).FirstOrDefaultAsync(cancellationToken);
        if (receipt is not null) return Ok(receipt);
        var invoice = await _context.Invoices.AsNoTracking().Where(x => x.InvoiceNumber == number &&
                x.VerificationCode == code)
            .Select(x => new { Valid = true, Kind = x.Type.ToString(), Number = x.InvoiceNumber,
                Status = x.Status.ToString(), Amount = x.TotalAmount, x.Currency, Date = x.IssueDate })
            .FirstOrDefaultAsync(cancellationToken);
        return invoice is null ? NotFound(new { Valid = false }) : Ok(invoice);
    }

    private async Task<object> InflowResponseAsync(Guid transactionId,
        CancellationToken cancellationToken)
    {
        return await (from transaction in _context.FinancialTransactions.AsNoTracking()
            join document in _context.TransactionDocuments.AsNoTracking()
                on transaction.Id equals document.FinancialTransactionId
            where transaction.Id == transactionId
            select new
            {
                Transaction = new FinancialTransactionListItem(transaction.Id,
                    transaction.TransactionNumber, transaction.Direction, transaction.Type,
                    transaction.Status, transaction.SourceType, transaction.SourceId,
                    transaction.CounterpartyName, transaction.Amount, transaction.Currency,
                    transaction.Method, transaction.SettlementAccountCode, transaction.BankAccountId,
                    transaction.ExternalReference, transaction.OccurredAt, transaction.BranchCode,
                    transaction.FreightClassification, transaction.JournalEntryId),
                Receipt = new TransactionDocumentListItem(document.Id, document.DocumentNumber,
                    document.Type, document.Status, document.FinancialTransactionId,
                    transaction.TransactionNumber, document.InvoiceId, document.PaymentId,
                    document.CompanyPurchaseId, document.PurchasePayoutId,
                    transaction.CounterpartyName, transaction.Amount, transaction.Currency,
                    transaction.Method, document.IssuedAt, document.VerificationCode)
            }).SingleAsync(cancellationToken);
    }

    private static string BuildTransactionDocumentHtml(string company, string address, string email,
        string documentNumber, string type, string counterparty, decimal amount, string currency,
        string method, string reference, string transactionNumber, string sourceNumber, DateTime issuedAt,
        string verificationCode)
    {
        static string encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
        return Header(company, type) + $"<h1>{encode(type)}</h1><div class='grid'>" +
            $"<div><b>Document:</b> {encode(documentNumber)}</div><div><b>Date:</b> {issuedAt:yyyy-MM-dd HH:mm} UTC</div>" +
            $"<div><b>Party:</b> {encode(counterparty)}</div><div><b>Method:</b> {encode(method)}</div>" +
            $"<div><b>Reference:</b> {encode(reference)}</div><div><b>Source:</b> {encode(sourceNumber)}</div></div>" +
            $"<div class='amount'>{encode(currency)} {amount:N2}</div><p>Transaction: {encode(transactionNumber)}</p>" +
            $"<p>{encode(address)}<br>{encode(email)}</p><p class='verify'>Verification code: {encode(verificationCode)}</p>" + Footer();
    }

    private static string Header(string company, string title) => "<!doctype html><html><head><meta charset='utf-8'>" +
        $"<title>{WebUtility.HtmlEncode(title)}</title><style>body{{font-family:Arial,sans-serif;margin:30px;color:#172033}}" +
        ".brand{font-size:22px;font-weight:700;border-bottom:3px solid #183b62;padding-bottom:10px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin:20px 0}" +
        "table{width:100%;border-collapse:collapse;margin:18px 0}th,td{border:1px solid #bbb;padding:8px}th{background:#eef3f8;text-align:left}.num{text-align:right}" +
        ".totals{margin-left:auto;width:300px;text-align:right;line-height:1.8}.amount{font-size:32px;font-weight:700;margin:25px 0}.verify{font-family:monospace}.footer{margin-top:30px;border-top:1px solid #bbb;padding-top:10px;font-size:12px}" +
        "@media print{button{display:none}body{margin:12mm}}</style></head><body><button onclick='window.print()'>Print</button>" +
        $"<div class='brand'>{WebUtility.HtmlEncode(company)}</div>";

    private static string Footer() => "<div class='footer'>Generated by the BiGSAM finance system. Verify the document number and security code before relying on it.</div></body></html>";
}
