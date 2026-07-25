using Application.Common.Interfaces;
using Application.Common;
using Api.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "FinanceTeam")]
[Route("api/finance")]
public sealed class FinanceController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IAccountingService _accounting;
    private readonly IClientCommunicationService _communications;
    private readonly IInvoiceNumberGenerator _invoiceNumbers;
    private readonly IFinancialPostingService _postings;
    private readonly IActiveBranchContext _branches;
    public FinanceController(IApplicationDbContext context, IAccountingService accounting,
        IClientCommunicationService communications, IInvoiceNumberGenerator invoiceNumbers,
        IFinancialPostingService postings, IActiveBranchContext branches)
    {
        _context = context;
        _accounting = accounting;
        _communications = communications;
        _invoiceNumbers = invoiceNumbers;
        _postings = postings;
        _branches = branches;
    }

    [HttpGet("invoices/{invoiceNumber:regex(^BGL\\d+INV$)}")]
    public async Task<ActionResult<InvoiceResponse>> GetInvoiceByNumber(string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var number = ClientIdentityNormalizer.Invoice(invoiceNumber);
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x =>
            x.InvoiceNumber == number && (branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken);
        if (invoice is null) return NotFound();
        var lines = await _context.InvoiceLines.AsNoTracking().Where(x => x.InvoiceId == invoice.Id)
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(Map(invoice, lines));
    }

    [HttpPatch("invoices/{invoiceNumber:regex(^BGL\\d+INV$)}")]
    public async Task<ActionResult<InvoiceResponse>> UpdateInvoiceByNumber(string invoiceNumber,
        UpdateInvoiceByNumberRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var number = ClientIdentityNormalizer.Invoice(invoiceNumber);
        var invoice = await _context.Invoices.FirstOrDefaultAsync(x =>
            x.InvoiceNumber == number && x.BranchId == branch.BranchId, cancellationToken);
        if (invoice is null) return NotFound();
        if (invoice.Status == InvoiceStatus.Void) return Conflict("A void invoice cannot be edited.");
        if (request.PreferredPaymentMethod.HasValue &&
            !PaymentRouting.IsSupported(request.PreferredPaymentMethod.Value))
            return BadRequest("Payment method must be Cash, BankTransfer, or Card.");
        invoice.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (request.PreferredPaymentMethod.HasValue)
            invoice.PreferredPaymentMethod = request.PreferredPaymentMethod.Value;
        await _context.SaveChangesAsync(cancellationToken);
        var lines = await _context.InvoiceLines.AsNoTracking().Where(x => x.InvoiceId == invoice.Id)
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(Map(invoice, lines));
    }

    [HttpPost("invoices/{invoiceNumber:regex(^BGL\\d+INV$)}/payments")]
    public async Task<ActionResult> RecordPaymentByInvoiceNumber(string invoiceNumber,
        RecordInvoicePaymentByNumberRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var number = ClientIdentityNormalizer.Invoice(invoiceNumber);
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x =>
            x.InvoiceNumber == number && x.BranchId == branch.BranchId, cancellationToken);
        if (invoice is null) return NotFound();
        var result = await _postings.RecordInvoicePaymentAsync(new InvoicePaymentCommand(invoice.Id,
            request.Reference, request.Amount, invoice.Currency, request.Method, request.BankAccountId,
            request.PaidAt, request.Notes, CurrentUserId), cancellationToken);
        return Created($"/api/finance/receipts/{result.Document.DocumentNumber}", new
        {
            result.Payment,
            Transaction = result.Transaction,
            Receipt = result.Document,
            DocumentUrl = $"/api/finance/receipts/{result.Document.DocumentNumber}/document?format=pdf"
        });
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<InvoiceResponse>> CreateInvoice(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0) return BadRequest("An invoice must contain at least one line.");
        if (request.DueDate.Date < request.IssueDate.Date) return BadRequest("Due date cannot be before issue date.");
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (customer is null) return BadRequest("Customer does not exist.");
        var issuer = await ResolveIssuerAsync(request.IssuerBranchCode, customer.CountryCode, cancellationToken);
        if (issuer is null) return BadRequest("Select an active issuer branch for this invoice.");
        var localCurrency = CountryCurrency.ForCountry(issuer.CountryCode);
        if (!string.IsNullOrWhiteSpace(request.Currency) &&
            !string.Equals(request.Currency, localCurrency, StringComparison.OrdinalIgnoreCase))
            return BadRequest($"The {issuer.Code} issuer branch creates invoices in {localCurrency}.");
        var originalError = await ValidateOriginalInvoiceAsync(request.Type, request.OriginalInvoiceId,
            customer.Id, cancellationToken);
        if (originalError is not null) return BadRequest(originalError);
        var methodError = ValidateInvoicePaymentMethod(request.Type, request.PreferredPaymentMethod);
        if (methodError is not null) return BadRequest(methodError);

        var invoice = new Invoice
        {
            CustomerId = request.CustomerId,
            CustomerName = customer.FullName,
            InvoiceNumber = await _invoiceNumbers.NextAsync(cancellationToken),
            Type = request.Type,
            OriginalInvoiceId = request.OriginalInvoiceId,
            IssuerBranchCode = issuer.Code,
            IssueDate = request.IssueDate.Date,
            DueDate = request.DueDate.Date,
            Currency = localCurrency,
            PaymentStatus = request.Type == InvoiceType.CreditNote
                ? InvoicePaymentStatus.NotApplicable : InvoicePaymentStatus.Unpaid,
            PreferredPaymentMethod = request.Type == InvoiceType.CreditNote
                ? null : request.PreferredPaymentMethod ?? PaymentMethod.BankTransfer,
            DiscountAmount = request.DiscountAmount,
            Notes = request.Notes?.Trim(),
            VerificationCode = DocumentSecurity.NewVerificationCode()
        };

        var lines = BuildLines(invoice, request.Lines);
        var totalError = CalculateTotals(invoice, lines);
        if (totalError is not null) return BadRequest(totalError);
        _context.Invoices.Add(invoice);
        _context.InvoiceLines.AddRange(lines);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, Map(invoice, lines));
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<InvoiceResponse>> GetInvoice(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken);
        if (invoice is null) return NotFound();
        var lines = await _context.InvoiceLines.AsNoTracking().Where(x => x.InvoiceId == id).ToListAsync(cancellationToken);
        return Ok(Map(invoice, lines));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult> GetInvoices(
        [FromQuery] InvoiceStatus? status, [FromQuery] InvoiceType? type,
        [FromQuery] InvoicePaymentStatus? paymentStatus, [FromQuery] Guid? customerId,
        [FromQuery] string? issuerBranchCode, [FromQuery] string? currency,
        [FromQuery] string? search, [FromQuery] DateTime? issueDateFrom, [FromQuery] DateTime? issueDateTo,
        [FromQuery] int take = 50,
        [FromQuery] bool paged = false, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.Invoices.AsNoTracking().Where(x =>
            activeBranch.IsAllBranches || x.BranchId == activeBranch.BranchId);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (paymentStatus.HasValue) query = query.Where(x => x.PaymentStatus == paymentStatus);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (activeBranch.IsAllBranches && !string.IsNullOrWhiteSpace(issuerBranchCode))
        {
            var branch = issuerBranchCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.IssuerBranchCode == branch);
        }
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var currencyCode = currency.Trim().ToUpperInvariant();
            query = query.Where(x => x.Currency == currencyCode);
        }
        if (issueDateFrom.HasValue) query = query.Where(x => x.IssueDate >= issueDateFrom.Value.Date);
        if (issueDateTo.HasValue) query = query.Where(x => x.IssueDate < issueDateTo.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.InvoiceNumber.ToLower().Contains(term) ||
                x.CustomerName.ToLower().Contains(term));
        }
        var ordered = query.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.Id);
        if (paged)
        {
            var result = await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
            return Ok(new PagedResponse<InvoiceResponse>(result.Items.Select(x =>
                    Map(x, Array.Empty<InvoiceLine>())).ToList(), result.Page, result.PageSize,
                result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
        }
        var invoices = await ordered.Take(Math.Clamp(take, 1, 100)).ToListAsync(cancellationToken);
        return Ok(invoices.Select(x => Map(x, Array.Empty<InvoiceLine>())).ToList());
    }

    [HttpPost("invoices/{id:guid}/issue")]
    public async Task<IActionResult> IssueInvoice(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var invoice = await _context.Invoices.FirstOrDefaultAsync(x =>
            x.Id == id && x.BranchId == branch.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft) return Conflict("Only draft invoices can be issued.");
        if (invoice.Type == InvoiceType.CreditNote)
        {
            var original = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == invoice.OriginalInvoiceId,
                cancellationToken) ?? throw new InvalidOperationException("Credit note original invoice was not found.");
            if (original.Type != InvoiceType.Sales || original.Status is InvoiceStatus.Draft or InvoiceStatus.Void)
                return Conflict("The original sales invoice must be issued before its credit note.");
            if (invoice.TotalAmount > original.BalanceDue)
                return Conflict("Credit note amount exceeds the outstanding original invoice balance.");
            invoice.Status = InvoiceStatus.Issued;
            invoice.PaymentStatus = InvoicePaymentStatus.NotApplicable;
            original.AppliedCreditAmount += invoice.TotalAmount;
            SyncPaymentState(original);
            var creditedRevenue = invoice.TotalAmount - invoice.TaxAmount;
            var reverseLines = new List<JournalPostingLine>
            {
                new("4000", creditedRevenue, 0, $"Credit revenue for {invoice.InvoiceNumber}"),
                new("1100", 0, invoice.TotalAmount, $"Credit receivable for {invoice.InvoiceNumber}")
            };
            if (invoice.TaxAmount > 0)
                reverseLines.Insert(1, new JournalPostingLine("2100", invoice.TaxAmount, 0,
                    $"Credit tax for {invoice.InvoiceNumber}"));
            await _accounting.PrepareJournalAsync("CreditNote", invoice.Id,
                $"Issue credit note {invoice.InvoiceNumber}", invoice.IssueDate, invoice.Currency,
                reverseLines, cancellationToken, branch.BranchId);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        invoice.Status = InvoiceStatus.Issued;
        invoice.PaymentStatus = InvoicePaymentStatus.Unpaid;
        var revenue = invoice.TotalAmount - invoice.TaxAmount;
        var lines = new List<JournalPostingLine>
        {
            new("1100", invoice.TotalAmount, 0, $"Receivable for {invoice.InvoiceNumber}"),
            new("4000", 0, revenue, $"Freight revenue for {invoice.InvoiceNumber}")
        };
        if (invoice.TaxAmount > 0) lines.Add(new("2100", 0, invoice.TaxAmount, $"Tax for {invoice.InvoiceNumber}"));
        await _accounting.PrepareJournalAsync("Invoice", invoice.Id, $"Issue invoice {invoice.InvoiceNumber}",
            invoice.IssueDate, invoice.Currency, lines, cancellationToken, branch.BranchId);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("invoices/{id:guid}/email")]
    public async Task<ActionResult> EmailInvoice(Guid id, SendClientEmailRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.BranchId == branch.BranchId, cancellationToken)
            ?? throw new KeyNotFoundException("Invoice not found.");
        if (invoice.Status == InvoiceStatus.Draft) return Conflict("Issue the invoice before emailing it.");
        var lines = await _context.InvoiceLines.AsNoTracking().Where(x => x.InvoiceId == id).ToListAsync(cancellationToken);
        var message = await _communications.SendAsync(invoice.CustomerId, CommunicationType.Invoice, request.Subject,
            request.Body, "Invoice", invoice.Id, request.AttachmentDocumentIds,
            new { invoice.InvoiceNumber, invoice.CustomerName, invoice.Type, invoice.IssuerBranchCode,
                invoice.PaymentStatus, invoice.IssueDate, invoice.DueDate, invoice.Currency, invoice.Subtotal,
                invoice.TaxAmount, invoice.DiscountAmount, invoice.TotalAmount, invoice.AmountPaid, invoice.BalanceDue,
                Lines = lines.Select(x => new { x.Description, x.Quantity, x.UnitPrice, x.TaxRate, x.LineTotal }) }, cancellationToken);
        return Ok(new { message.Id, message.Status, invoice.InvoiceNumber, Accepted = true });
    }

    [HttpPost("payments")]
    public async Task<ActionResult> RecordPayment(RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == request.InvoiceId && x.BranchId == branch.BranchId, cancellationToken);
        if (invoice is null) return NotFound("Invoice not found in the active branch.");
        var result = await _postings.RecordInvoicePaymentAsync(new InvoicePaymentCommand(request.InvoiceId,
            request.Reference, request.Amount, invoice.Currency, request.Method, request.BankAccountId,
            request.PaidAt, request.Notes, CurrentUserId), cancellationToken);
        return Created($"/api/finance/receipts/{result.Document.Id}", new
        {
            result.Payment,
            Transaction = result.Transaction,
            Receipt = result.Document,
            PrintUrl = $"/api/finance/receipts/{result.Document.Id}/print"
        });
    }

    [HttpPost("expenses")]
    public async Task<ActionResult<Expense>> CreateExpense(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return BadRequest("Expense amount must be greater than zero.");
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        if (!string.Equals(request.BranchCode, branch.Code, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The expense branch must match the active branch.");
        var localCurrency = branch.Currency ?? CountryCurrency.ForCountry(branch.CountryCode);
        if (!string.Equals(request.Currency, localCurrency, StringComparison.OrdinalIgnoreCase))
            return BadRequest($"Expenses for this branch must be calculated in {localCurrency}.");
        var expense = new Expense
        {
            ExpenseNumber = $"BGL-EXP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Category = request.Category.Trim(),
            Description = request.Description.Trim(),
            Payee = request.Payee.Trim(),
            Amount = request.Amount,
            Currency = localCurrency,
            IncurredAt = request.IncurredAt,
            BranchCode = branch.Code!,
            SubmittedByUserId = CurrentUserId,
            Status = ExpenseStatus.Submitted
        };
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/finance/expenses/{expense.Id}", expense);
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPatch("expenses/{id:guid}/decision")]
    public async Task<IActionResult> DecideExpense(Guid id, ExpenseDecisionRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not (ExpenseStatus.Approved or ExpenseStatus.Rejected))
            return BadRequest("Decision must be Approved or Rejected.");
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var expense = await _context.Expenses.FirstOrDefaultAsync(x =>
            x.Id == id && x.BranchCode == branch.Code, cancellationToken)
            ?? throw new KeyNotFoundException("Expense not found.");
        if (expense.Status != ExpenseStatus.Submitted) return Conflict("Only submitted expenses can be decided.");
        expense.Status = request.Status;
        expense.ApprovedByUserId = CurrentUserId;
        expense.ApprovedAt = DateTime.UtcNow;
        if (request.Status == ExpenseStatus.Approved)
            await _accounting.PrepareJournalAsync("Expense", expense.Id, $"Approved expense {expense.ExpenseNumber}",
                expense.IncurredAt, expense.Currency, new[]
                {
                    new JournalPostingLine("5000", expense.Amount, 0, expense.Description),
                    new JournalPostingLine("2000", 0, expense.Amount, expense.Payee)
                }, cancellationToken, branch.BranchId);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<Branch?> ResolveIssuerAsync(string? requestedBranchCode, string customerCountryCode,
        CancellationToken cancellationToken)
    {
        var staffBranchCode = await _context.StaffProfiles.AsNoTracking()
            .Where(x => x.UserAccountId == CurrentUserId).Select(x => x.BranchCode)
            .FirstOrDefaultAsync(cancellationToken);
        var selectedCode = !string.IsNullOrWhiteSpace(staffBranchCode) && staffBranchCode != "GLOBAL"
            ? staffBranchCode
            : requestedBranchCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(selectedCode))
            return await _context.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Code == selectedCode && x.IsActive,
                cancellationToken);
        var normalizedCountry = customerCountryCode.Trim().ToUpperInvariant();
        var matches = await _context.Branches.AsNoTracking().Where(x => x.IsActive &&
                (x.CountryCode == normalizedCountry ||
                 (normalizedCountry == "NGA" && x.CountryCode == "NG") ||
                 (normalizedCountry == "CHN" && x.CountryCode == "CN") ||
                 ((normalizedCountry == "GBR" || normalizedCountry == "UK") && x.CountryCode == "GB")))
            .Take(2).ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<string?> ValidateOriginalInvoiceAsync(InvoiceType type, Guid? originalInvoiceId,
        Guid customerId, CancellationToken cancellationToken)
    {
        if (type == InvoiceType.Sales && originalInvoiceId.HasValue)
            return "A sales invoice cannot reference an original invoice.";
        if (type == InvoiceType.CreditNote && !originalInvoiceId.HasValue)
            return "A credit note must reference its original sales invoice.";
        if (!originalInvoiceId.HasValue) return null;
        var original = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == originalInvoiceId,
            cancellationToken);
        if (original is null || original.Type != InvoiceType.Sales)
            return "Original sales invoice was not found.";
        return original.CustomerId != customerId ? "Credit note and original invoice must belong to the same client." : null;
    }

    private static List<InvoiceLine> BuildLines(Invoice invoice, IReadOnlyList<CreateInvoiceLineRequest> requested) =>
        requested.Select(line =>
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Quantity <= 0 ||
                line.UnitPrice < 0 || line.TaxRate is < 0 or > 1)
                throw new ArgumentException("Invoice line description, quantity, price, and tax rate are invalid.");
            var beforeTax = Math.Round(line.Quantity * line.UnitPrice, 2);
            var tax = Math.Round(beforeTax * line.TaxRate, 2);
            return new InvoiceLine { InvoiceId = invoice.Id, Description = line.Description.Trim(),
                Quantity = line.Quantity, UnitPrice = line.UnitPrice, TaxRate = line.TaxRate,
                LineTotal = beforeTax + tax };
        }).ToList();

    private static string? CalculateTotals(Invoice invoice, IReadOnlyCollection<InvoiceLine> lines)
    {
        if (invoice.DiscountAmount < 0) return "Discount cannot be negative.";
        invoice.Subtotal = lines.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2));
        invoice.TaxAmount = lines.Sum(x => x.LineTotal - Math.Round(x.Quantity * x.UnitPrice, 2));
        invoice.TotalAmount = Math.Max(0, invoice.Subtotal + invoice.TaxAmount - invoice.DiscountAmount);
        return invoice.TotalAmount <= 0 ? "Invoice total must be greater than zero." : null;
    }

    private static string? ValidateInvoicePaymentMethod(InvoiceType type, PaymentMethod? method)
    {
        if (type == InvoiceType.CreditNote && method.HasValue)
            return "Credit notes do not have a payment method.";
        if (type == InvoiceType.Sales && method.HasValue && !PaymentRouting.IsSupported(method.Value))
            return "Invoice payment method must be Cash, BankTransfer, or Card.";
        return null;
    }

    private static void SyncPaymentState(Invoice invoice)
    {
        if (invoice.Type == InvoiceType.CreditNote)
        {
            invoice.PaymentStatus = InvoicePaymentStatus.NotApplicable;
            return;
        }
        if (invoice.BalanceDue == 0)
        {
            invoice.PaymentStatus = InvoicePaymentStatus.Paid;
            invoice.Status = InvoiceStatus.Paid;
        }
        else if (invoice.AmountPaid > 0 || invoice.AppliedCreditAmount > 0)
        {
            invoice.PaymentStatus = InvoicePaymentStatus.PartiallyPaid;
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }
        else
        {
            invoice.PaymentStatus = InvoicePaymentStatus.Unpaid;
            if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.PartiallyPaid) invoice.Status = InvoiceStatus.Issued;
        }
    }

    private static InvoiceResponse Map(Invoice invoice, IReadOnlyCollection<InvoiceLine> lines) => new(
        invoice.Id, invoice.InvoiceNumber, invoice.CustomerId, invoice.CustomerName, invoice.Type,
        invoice.OriginalInvoiceId, invoice.IssuerBranchCode, invoice.IssueDate, invoice.DueDate,
        invoice.Status, invoice.PaymentStatus, invoice.PreferredPaymentMethod, invoice.Currency,
        invoice.Subtotal, invoice.TaxAmount,
        invoice.DiscountAmount, invoice.TotalAmount, invoice.AmountPaid, invoice.AppliedCreditAmount,
        invoice.BalanceDue, invoice.Notes, invoice.VerificationCode, invoice.LastModifiedAt,
        lines.Select(x => new InvoiceLineResponse(x.Id, x.Description, x.Quantity, x.UnitPrice, x.TaxRate, x.LineTotal)).ToList());
}
