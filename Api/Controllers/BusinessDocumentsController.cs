using System.Globalization;
using Api.Services;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>Downloadable, number-addressed commercial documents for the dashboard.</summary>
[Authorize(Policy = "StaffOnly")]
[Route("api")]
public sealed class BusinessDocumentsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IOfficeDocumentService _documents;
    private readonly IActiveBranchContext _branches;

    public BusinessDocumentsController(IApplicationDbContext context, IOfficeDocumentService documents,
        IActiveBranchContext branches)
    {
        _context = context;
        _documents = documents;
        _branches = branches;
    }

    [HttpGet("finance/invoices/{invoiceNumber:regex(^BGL\\d+INV$)}/document")]
    public async Task<IActionResult> Invoice(string invoiceNumber,
        [FromQuery] BusinessDocumentFormat format = BusinessDocumentFormat.Pdf,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var number = invoiceNumber.Trim().ToUpperInvariant();
        var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x =>
            x.InvoiceNumber == number && (branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken);
        if (invoice is null) return NotFound();
        var lines = await _context.InvoiceLines.AsNoTracking().Where(x => x.InvoiceId == invoice.Id)
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var company = await CompanyName(cancellationToken);
        var table = new OfficeSheet("Invoice lines",
            ["Description", "Quantity", "Unit", "Unit price", "Tax rate", "Line total"],
            lines.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.Description), new(x.Quantity, OfficeCellKind.Number), new(x.Unit),
                new(x.UnitPrice, OfficeCellKind.Money), new($"{x.TaxRate:P2}"),
                new(x.LineTotal, OfficeCellKind.Money)
            ]).ToList());
        var document = new BusinessDocument($"{invoice.Type} invoice", invoice.InvoiceNumber, company,
        [
            Pair("Client", invoice.CustomerName),
            Pair("Issuer branch", invoice.IssuerBranchCode),
            Pair("Issue date", invoice.IssueDate.ToString("yyyy-MM-dd")),
            Pair("Payment state", invoice.PaymentStatus.ToString()),
            Pair("Payment method", invoice.PreferredPaymentMethod?.ToString() ?? "Not applicable"),
            Pair("Currency", invoice.Currency)
        ], table,
        [
            Pair("Subtotal", Money(invoice.Currency, invoice.Subtotal)),
            Pair("Tax", Money(invoice.Currency, invoice.TaxAmount)),
            Pair("Discount", Money(invoice.Currency, invoice.DiscountAmount)),
            Pair("Total", Money(invoice.Currency, invoice.TotalAmount)),
            Pair("Paid", Money(invoice.Currency, invoice.AmountPaid)),
            Pair("Credit applied", Money(invoice.Currency, invoice.AppliedCreditAmount)),
            Pair("Balance", Money(invoice.Currency, invoice.BalanceDue))
        ], invoice.VerificationCode,
        "Generated from the protected shipment invoice. The invoice number is immutable.");
        return Download(document, format, invoice.InvoiceNumber);
    }

    [HttpGet("documents/invoices/{invoiceId:guid}/pdf")]
    public async Task<IActionResult> InvoicePdf(Guid invoiceId,
        CancellationToken cancellationToken = default)
        => await InvoiceById(invoiceId, BusinessDocumentFormat.Pdf, cancellationToken);

    [HttpGet("documents/invoices/{invoiceId:guid}")]
    public async Task<IActionResult> InvoiceById(Guid invoiceId,
        [FromQuery] BusinessDocumentFormat format = BusinessDocumentFormat.Pdf,
        CancellationToken cancellationToken = default)
    {
        var number = await _context.Invoices.AsNoTracking().Where(x => x.Id == invoiceId)
            .Select(x => x.InvoiceNumber).SingleOrDefaultAsync(cancellationToken);
        return number is null ? NotFound() :
            await Invoice(number, format, cancellationToken);
    }

    [HttpGet("finance/receipts/{receiptNumber:regex(^BGL\\d+RCT$)}/document")]
    public async Task<IActionResult> Receipt(string receiptNumber,
        [FromQuery] BusinessDocumentFormat format = BusinessDocumentFormat.Pdf,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var number = receiptNumber.Trim().ToUpperInvariant();
        var data = await (from document in _context.TransactionDocuments.AsNoTracking()
            join transaction in _context.FinancialTransactions.AsNoTracking()
                on document.FinancialTransactionId equals transaction.Id
            join invoice in _context.Invoices.AsNoTracking()
                on document.InvoiceId equals (Guid?)invoice.Id into invoiceRows
            from invoice in invoiceRows.DefaultIfEmpty()
            where document.DocumentNumber == number &&
                  (branch.IsAllBranches || transaction.BranchId == branch.BranchId)
            select new { Document = document, Transaction = transaction, Invoice = invoice })
            .FirstOrDefaultAsync(cancellationToken);
        if (data is null) return NotFound();
        var company = await CompanyName(cancellationToken);
        var documentData = new BusinessDocument("Payment receipt", data.Document.DocumentNumber, company,
        [
            Pair("Client", data.Transaction.CounterpartyName),
            Pair("Invoice", data.Invoice == null ? "Not linked" : data.Invoice.InvoiceNumber),
            Pair("Transaction", data.Transaction.TransactionNumber),
            Pair("Branch", data.Transaction.BranchCode),
            Pair("Issued", data.Document.IssuedAt.ToString("yyyy-MM-dd HH:mm 'UTC'")),
            Pair("Payment method", data.Transaction.Method.ToString()),
            Pair("Payment reference", data.Transaction.ExternalReference),
            Pair("Status", data.Document.Status.ToString())
        ], null,
        [
            Pair("Amount received", Money(data.Transaction.Currency, data.Transaction.Amount))
        ], data.Document.VerificationCode,
        data.Document.Status == FinancialDocumentStatus.Voided
            ? $"VOID: {data.Document.VoidReason}" : "This receipt records a posted customer payment.");
        return Download(documentData, format, data.Document.DocumentNumber);
    }

    [HttpGet("documents/receipts/{receiptId:guid}/pdf")]
    public async Task<IActionResult> ReceiptPdf(Guid receiptId,
        CancellationToken cancellationToken = default)
        => await ReceiptById(receiptId, BusinessDocumentFormat.Pdf, cancellationToken);

    [HttpGet("documents/receipts/{receiptId:guid}")]
    public async Task<IActionResult> ReceiptById(Guid receiptId,
        [FromQuery] BusinessDocumentFormat format = BusinessDocumentFormat.Pdf,
        CancellationToken cancellationToken = default)
    {
        var number = await _context.TransactionDocuments.AsNoTracking().Where(x => x.Id == receiptId)
            .Select(x => x.DocumentNumber).SingleOrDefaultAsync(cancellationToken);
        return number is null ? NotFound() :
            await Receipt(number, format, cancellationToken);
    }

    [HttpGet("finance/credit-notes/{creditNoteId:guid}/document")]
    [HttpGet("documents/credit-notes/{creditNoteId:guid}")]
    [HttpGet("documents/credit-notes/{creditNoteId:guid}/pdf")]
    public async Task<IActionResult> CreditNote(Guid creditNoteId,
        [FromQuery] BusinessDocumentFormat format = BusinessDocumentFormat.Pdf,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var data = await (from credit in _context.CreditNotes.AsNoTracking()
            join customer in _context.Customers.AsNoTracking() on credit.CustomerId equals customer.Id
            where credit.Id == creditNoteId &&
                  (branch.IsAllBranches || credit.BranchId == branch.BranchId)
            select new { Credit = credit, Customer = customer }).FirstOrDefaultAsync(cancellationToken);
        if (data is null) return NotFound();
        var company = await CompanyName(cancellationToken);
        var document = new BusinessDocument("Credit note", data.Credit.CreditNoteNumber, company,
        [
            Pair("Client", data.Customer.FullName),
            Pair("Issued", data.Credit.IssuedAt.ToString("yyyy-MM-dd HH:mm 'UTC'")),
            Pair("State", data.Credit.State.ToString()),
            Pair("Reason", data.Credit.Reason)
        ], null,
        [
            Pair("Original credit", Money(data.Credit.Currency, data.Credit.OriginalAmount)),
            Pair("Used", Money(data.Credit.Currency, data.Credit.UsedAmount)),
            Pair("Available", Money(data.Credit.Currency, data.Credit.AvailableAmount))
        ], Footer: "A credit note offsets a future shipment invoice; it is not a cash refund.");
        return Download(document, format, data.Credit.CreditNoteNumber);
    }

    [HttpGet("sales/quotations/{quotationNumber}/document")]
    public async Task<IActionResult> Quotation(string quotationNumber,
        [FromQuery] BusinessDocumentFormat format = BusinessDocumentFormat.Pdf,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var number = quotationNumber.Trim().ToUpperInvariant();
        var data = await (from quote in _context.ShippingQuotes.AsNoTracking()
            join route in _context.ServiceRoutes.AsNoTracking() on quote.ServiceRouteId equals route.Id
            where quote.QuoteNumber == number &&
                  (branch.IsAllBranches || route.OriginBranchId == branch.BranchId)
            select new { Quote = quote, Route = route }).FirstOrDefaultAsync(cancellationToken);
        if (data is null) return NotFound();
        var company = await CompanyName(cancellationToken);
        var document = new BusinessDocument("Shipping quotation", data.Quote.QuoteNumber, company,
        [
            Pair("Client", data.Quote.ContactName),
            Pair("Email", data.Quote.ContactEmail),
            Pair("Phone", data.Quote.ContactPhone),
            Pair("Route", data.Route.Name),
            Pair("Method", data.Route.Mode.ToString()),
            Pair("Cargo", data.Quote.CargoDescription),
            Pair("Billable weight", $"{data.Quote.BillableWeightKg:N2} kg"),
            Pair("Expires", data.Quote.ExpiresAt.ToString("yyyy-MM-dd")),
            Pair("Status", data.Quote.Status.ToString())
        ], null,
        [
            Pair("Base amount", Money(data.Quote.Currency, data.Quote.BaseAmount)),
            Pair("Fuel surcharge", Money(data.Quote.Currency, data.Quote.FuelSurchargeAmount)),
            Pair("Customs", Money(data.Quote.Currency, data.Quote.CustomsAmount)),
            Pair("Quoted total", Money(data.Quote.Currency, data.Quote.TotalAmount))
        ], Footer: "Prices remain subject to the validity period and verified shipment details.");
        return Download(document, format, data.Quote.QuoteNumber);
    }

    private IActionResult Download(BusinessDocument document, BusinessDocumentFormat format, string number)
    {
        var rendered = _documents.Render(document, format);
        return File(rendered.Content, rendered.ContentType,
            $"{number.ToUpperInvariant()}.{rendered.FileExtension}");
    }

    private async Task<string> CompanyName(CancellationToken cancellationToken) =>
        await _context.CompanyProfiles.AsNoTracking().OrderBy(x => x.Id).Select(x => x.TradingName)
            .FirstOrDefaultAsync(cancellationToken) ?? "BiGSAM Global Logistics";

    private static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);
    private static string Money(string currency, decimal amount) =>
        $"{currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
}
