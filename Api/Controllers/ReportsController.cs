using System.Globalization;
using System.Text;
using System.Text.Json;
using Api.Services;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[EnableRateLimiting("Reports")]
[RequestTimeout("Reports")]
[Route("api/reports")]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    private readonly IOfficeDocumentService _documents;
    public ReportsController(IApplicationDbContext context, IActiveBranchContext branches,
        IOfficeDocumentService documents)
    {
        _context = context;
        _branches = branches;
        _documents = documents;
    }

    [HttpGet("catalog")]
    public ActionResult Catalog() => Ok(new[]
    {
        new { Key = "executive-summary", Name = "Executive summary",
            Description = "Branch and currency separated sales, payments, purchases and operations." },
        new { Key = "sales", Name = "Sales and receivables",
            Description = "Invoices, payment state, collected amounts and outstanding balances." },
        new { Key = "purchases", Name = "Purchases and payables",
            Description = "Input, output and service purchases with payment state." },
        new { Key = "inventory", Name = "Basic inventory",
            Description = "Stock on hand, reservations and reorder warnings by hub." },
        new { Key = "schedule", Name = "Schedule",
            Description = "Pickups, meetings and maintenance timing." },
        new { Key = "operations", Name = "Logistics operations",
            Description = "Invoice-linked customer shipments, routes, handover and movement state." },
        new { Key = "finance", Name = "Financial ledger",
            Description = "Credits and debits separated by currency, branch and freight method." },
        new { Key = "clients", Name = "Client contacts",
            Description = "Minimal client directory and contact completeness." },
        new { Key = "staff", Name = "Staff access",
            Description = "Staff directory, account access and branch assignment." }
    });

    [HttpPost("preview")]
    public async Task<ActionResult<ReportPreviewResponse>> Preview(ReportRequest request,
        CancellationToken cancellationToken)
    {
        var workbook = await BuildWorkbook(request, Math.Clamp(request.PreviewRows, 10, 250),
            cancellationToken);
        return Ok(new ReportPreviewResponse(NormalizeReportKey(request.ReportKey), workbook.GeneratedAt,
            workbook.Sheets.Select(sheet => new ReportPreviewSheet(sheet.Name, sheet.Columns,
                sheet.Rows.Select(row => (IReadOnlyList<object?>)row.Select(x => x.Value).ToList())
                    .ToList(), sheet.Rows.Count)).ToList()));
    }

    [HttpPost("exports")]
    public async Task<ActionResult<ReportExportJobResponse>> CreateExport(ReportRequest request,
        CancellationToken cancellationToken)
    {
        var key = NormalizeReportKey(request.ReportKey);
        var job = new ReportExportJob
        {
            RequestedByUserId = CurrentUserId,
            ReportKey = key,
            FiltersJson = JsonSerializer.Serialize(request),
            Status = ReportExportStatus.Processing,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            FileName = $"bigsam-{key}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx"
        };
        _context.ReportExportJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        try
        {
            var workbook = await BuildWorkbook(request with { ReportKey = key }, 50_000,
                cancellationToken);
            var content = _documents.RenderWorkbook(workbook);
            if (content.Length > 25 * 1024 * 1024)
                throw new InvalidOperationException(
                    "The export exceeds 25 MB. Narrow the branch or date filters and try again.");
            job.FileContent = content;
            job.Status = ReportExportStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            job.Status = ReportExportStatus.Failed;
            job.ErrorMessage = exception.Message;
            job.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        var response = MapJob(job);
        return Created($"/api/reports/exports/{job.Id}", response);
    }

    [HttpGet("exports/{jobId:guid}")]
    public async Task<ActionResult<ReportExportJobResponse>> GetExport(Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await _context.ReportExportJobs.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == jobId && x.RequestedByUserId == CurrentUserId, cancellationToken);
        return job is null ? NotFound() : Ok(MapJob(job));
    }

    [HttpGet("exports/{jobId:guid}/download")]
    public async Task<IActionResult> DownloadExport(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _context.ReportExportJobs.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == jobId && x.RequestedByUserId == CurrentUserId, cancellationToken);
        if (job is null) return NotFound();
        if (job.ExpiresAt <= DateTime.UtcNow) return StatusCode(StatusCodes.Status410Gone,
            "This export has expired. Generate it again.");
        if (job.Status != ReportExportStatus.Completed || job.FileContent is null)
            return Conflict("The report export is not ready for download.");
        return File(job.FileContent, job.ContentType, job.FileName);
    }

    [HttpDelete("exports/{jobId:guid}")]
    public async Task<IActionResult> DeleteExport(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _context.ReportExportJobs.FirstOrDefaultAsync(x =>
            x.Id == jobId && x.RequestedByUserId == CurrentUserId, cancellationToken);
        if (job is null) return NotFound();
        _context.ReportExportJobs.Remove(job);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("executive-summary")]
    public async Task<ActionResult> ExecutiveSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var (start, end, error) = Period(from, to);
        if (error is not null) return BadRequest(error);
        var quotations = await _context.ShippingQuotes.AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt <= end)
            .GroupBy(x => new { x.Currency, x.Status })
            .Select(x => new { x.Key.Currency, x.Key.Status, Count = x.Count(), Value = x.Sum(y => y.TotalAmount) })
            .ToListAsync(cancellationToken);
        var invoices = await _context.Invoices.AsNoTracking()
            .Where(x => x.IssueDate >= start && x.IssueDate <= end && x.Status != InvoiceStatus.Void)
            .GroupBy(x => x.Currency).Select(x => new { Currency = x.Key, Count = x.Count(),
                Invoiced = x.Sum(y => y.TotalAmount), Paid = x.Sum(y => y.AmountPaid) })
            .ToListAsync(cancellationToken);
        var purchases = await _context.CompanyPurchases.AsNoTracking()
            .Where(x => x.PurchaseDate >= start && x.PurchaseDate <= end && x.Status != CompanyPurchaseStatus.Cancelled &&
                x.Status != CompanyPurchaseStatus.Rejected)
            .GroupBy(x => x.Currency).Select(x => new { Currency = x.Key, Count = x.Count(),
                Purchased = x.Sum(y => y.TotalAmount), Paid = x.Sum(y => y.AmountPaid) })
            .ToListAsync(cancellationToken);
        var appointmentStatus = await _context.Appointments.AsNoTracking()
            .Where(x => x.StartsAt >= start && x.StartsAt <= end).GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(cancellationToken);
        var activeCargo = await _context.CargoItems.CountAsync(x => x.Status != CargoStatus.Delivered &&
            x.Status != CargoStatus.Cancelled, cancellationToken);
        var stockRows = await _context.InventoryStockLevels.AsNoTracking()
            .Select(x => new { x.InventoryItemId, x.QuantityOnHand, x.QuantityReserved, x.ReorderLevel })
            .ToListAsync(cancellationToken);
        var contacts = await _context.Contacts.AsNoTracking().Where(x => x.Status != ContactStatus.Archived)
            .GroupBy(x => x.Type).Select(x => new { Type = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        return Ok(new
        {
            From = start, To = end, GeneratedAt = DateTime.UtcNow,
            Quotations = quotations, InvoicesByCurrency = invoices, PurchasesByCurrency = purchases,
            Appointments = appointmentStatus, ActiveCargo = activeCargo,
            Inventory = new { ItemCount = stockRows.Select(x => x.InventoryItemId).Distinct().Count(),
                UnitsOnHand = stockRows.Sum(x => x.QuantityOnHand),
                LowStockLines = stockRows.Count(x => x.QuantityOnHand - x.QuantityReserved <= x.ReorderLevel) },
            Contacts = contacts
        });
    }

    [HttpGet("sales")]
    public async Task<ActionResult> Sales([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? branchCode, CancellationToken cancellationToken)
    {
        var (start, end, error) = Period(from, to);
        if (error is not null) return BadRequest(error);
        var invoiceQuery = _context.Invoices.AsNoTracking().Where(x => x.IssueDate >= start &&
            x.IssueDate <= end && x.Status != InvoiceStatus.Void);
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant();
            invoiceQuery = invoiceQuery.Where(x => x.IssuerBranchCode == value);
        }
        var invoices = await invoiceQuery.ToListAsync(cancellationToken);
        var quotes = await _context.ShippingQuotes.AsNoTracking().Where(x => x.CreatedAt >= start &&
            x.CreatedAt <= end).ToListAsync(cancellationToken);
        var receipts = await (from document in _context.TransactionDocuments.AsNoTracking()
                              join transaction in _context.FinancialTransactions.AsNoTracking()
                                  on document.FinancialTransactionId equals transaction.Id
                              where document.Type == FinancialDocumentType.Receipt &&
                                    document.Status == FinancialDocumentStatus.Issued &&
                                    document.IssuedAt >= start && document.IssuedAt <= end
                              select new { transaction.Currency, transaction.Amount,
                                  transaction.Method, transaction.BranchCode }).ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant();
            receipts = receipts.Where(x => x.BranchCode == value).ToList();
        }
        return Ok(new
        {
            From = start, To = end,
            Invoices = invoices.GroupBy(x => x.Currency).Select(x => new
            {
                Currency = x.Key, SalesCount = x.Count(y => y.Type == InvoiceType.Sales),
                SalesValue = x.Where(y => y.Type == InvoiceType.Sales).Sum(y => y.TotalAmount),
                CreditNoteValue = x.Where(y => y.Type == InvoiceType.CreditNote).Sum(y => y.TotalAmount),
                Paid = x.Sum(y => y.AmountPaid),
                Outstanding = x.Where(y => y.Type == InvoiceType.Sales)
                    .Sum(y => Math.Max(0, y.TotalAmount - y.AmountPaid - y.AppliedCreditAmount))
            }),
            Quotations = quotes.GroupBy(x => x.Currency).Select(x => new
            {
                Currency = x.Key, Count = x.Count(), Value = x.Sum(y => y.TotalAmount),
                Accepted = x.Count(y => y.Status is QuoteStatus.Accepted or QuoteStatus.Converted),
                AcceptedValue = x.Where(y => y.Status is QuoteStatus.Accepted or QuoteStatus.Converted)
                    .Sum(y => y.TotalAmount)
            }),
            Receipts = receipts.GroupBy(x => new { x.Currency, x.Method }).Select(x => new
                { x.Key.Currency, x.Key.Method, Count = x.Count(), Amount = x.Sum(y => y.Amount) })
        });
    }

    [HttpGet("purchases")]
    public async Task<ActionResult> Purchases([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? currency, [FromQuery] string? branchCode,
        CancellationToken cancellationToken)
    {
        var (start, end, error) = Period(from, to);
        if (error is not null) return BadRequest(error);
        var query = _context.CompanyPurchases.AsNoTracking()
            .Where(x => x.PurchaseDate >= start && x.PurchaseDate <= end);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Currency == value);
        }
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant(); query = query.Where(x => x.BranchCode == value);
        }
        var rows = await query.ToListAsync(cancellationToken);
        var purchaseIds = rows.Select(x => x.Id).ToArray();
        var payouts = await _context.PurchasePayouts.AsNoTracking().Where(x =>
                purchaseIds.Contains(x.CompanyPurchaseId) && x.Status == PurchasePayoutStatus.Confirmed)
            .ToListAsync(cancellationToken);
        return Ok(new
        {
            From = start, To = end,
            ByCurrency = rows.GroupBy(x => x.Currency).Select(x => new { Currency = x.Key,
                Count = x.Count(), Total = x.Sum(y => y.TotalAmount), Paid = x.Sum(y => y.AmountPaid),
                Outstanding = x.Sum(y => y.TotalAmount - y.AmountPaid) }),
            ByCategory = rows.GroupBy(x => new { x.Currency, x.Category }).Select(x => new
                { x.Key.Currency, x.Key.Category, Count = x.Count(), Total = x.Sum(y => y.TotalAmount) }),
            ByType = rows.GroupBy(x => new { x.Currency, x.Type }).Select(x => new
                { x.Key.Currency, x.Key.Type, Count = x.Count(), Total = x.Sum(y => y.TotalAmount) }),
            Payments = payouts.GroupBy(x => new { x.Currency, x.Method }).Select(x => new
                { x.Key.Currency, x.Key.Method, Count = x.Count(), Amount = x.Sum(y => y.Amount) }),
            ByStatus = rows.GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }),
            Receiving = rows.GroupBy(x => x.ReceivingStatus).Select(x => new { Status = x.Key, Count = x.Count() })
        });
    }

    [HttpGet("inventory")]
    public async Task<ActionResult> Inventory([FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var query = from stock in _context.InventoryStockLevels.AsNoTracking()
                    join item in _context.InventoryItems.AsNoTracking() on stock.InventoryItemId equals item.Id
                    join location in _context.InventoryLocations.AsNoTracking() on stock.InventoryLocationId equals location.Id
                    select new { ItemId = item.Id, item.Sku, item.Name, item.Category,
                        location.BranchId, LocationId = location.Id, LocationName = location.Name,
                        stock.QuantityOnHand, stock.QuantityReserved, stock.ReorderLevel };
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        var rows = await query.ToListAsync(cancellationToken);
        return Ok(new
        {
            ItemCount = rows.Select(x => x.ItemId).Distinct().Count(),
            TotalUnits = rows.Sum(x => x.QuantityOnHand),
            ByCategory = rows.GroupBy(x => x.Category).Select(x => new
                { Category = x.Key, Items = x.Select(y => y.ItemId).Distinct().Count(),
                    Units = x.Sum(y => y.QuantityOnHand) }),
            LowStock = rows.Where(x => x.QuantityOnHand - x.QuantityReserved <= x.ReorderLevel)
                .OrderBy(x => x.Name).Select(x => new { x.ItemId, x.Sku, x.Name, x.LocationId,
                    x.LocationName, Available = x.QuantityOnHand - x.QuantityReserved,
                    x.ReorderLevel })
        });
    }

    [HttpGet("schedule")]
    [HttpGet("appointments")]
    public async Task<ActionResult> Appointments([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? staffUserId, CancellationToken cancellationToken)
    {
        var (start, end, error) = Period(from, to);
        if (error is not null) return BadRequest(error);
        var query = _context.Appointments.AsNoTracking()
            .Where(x => x.StartsAt >= start && x.StartsAt <= end);
        if (staffUserId.HasValue)
        {
            var staff = staffUserId.Value;
            var attendeeIds = _context.AppointmentAttendees.AsNoTracking().Where(x => x.UserAccountId == staff)
                .Select(x => x.AppointmentId);
            query = query.Where(x => x.OrganizerUserId == staff || x.AssignedUserId == staff || attendeeIds.Contains(x.Id));
        }
        var rows = await query.ToListAsync(cancellationToken);
        var completed = rows.Count(x => x.Status == AppointmentStatus.Completed);
        var closed = completed + rows.Count(x => x.Status == AppointmentStatus.NoShow);
        return Ok(new { From = start, To = end, Total = rows.Count,
            CompletionRate = closed == 0 ? 0 : Math.Round(completed * 100m / closed, 2),
            ByStatus = rows.GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }),
            ByType = rows.GroupBy(x => x.Type).Select(x => new { Type = x.Key, Count = x.Count() }) });
    }

    [HttpGet("contacts")]
    public async Task<ActionResult> Contacts(CancellationToken cancellationToken)
    {
        var contacts = await _context.Contacts.AsNoTracking().Where(x => x.Status != ContactStatus.Archived)
            .Select(x => new { x.Type, x.Status, x.CountryCode }).ToListAsync(cancellationToken);
        return Ok(new
        {
            Total = contacts.Count,
            ByType = contacts.GroupBy(x => x.Type).Select(x => new
                { Type = x.Key, Count = x.Count(), Active = x.Count(y => y.Status == ContactStatus.Active) }),
            ByCountry = contacts.GroupBy(x => x.CountryCode).Select(x => new
                { CountryCode = x.Key, Count = x.Count() })
        });
    }

    [HttpGet("inventory/export.csv")]
    public async Task<IActionResult> ExportInventory([FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var query = from stock in _context.InventoryStockLevels.AsNoTracking()
                    join item in _context.InventoryItems.AsNoTracking() on stock.InventoryItemId equals item.Id
                    join location in _context.InventoryLocations.AsNoTracking() on stock.InventoryLocationId equals location.Id
                    join branch in _context.Branches.AsNoTracking() on location.BranchId equals branch.Id
                    select new { item.Sku, item.Name, item.Category, Branch = branch.Code,
                        Location = location.Name, stock.QuantityOnHand, stock.QuantityReserved,
                        Available = stock.QuantityOnHand - stock.QuantityReserved, stock.ReorderLevel };
        if (branchId.HasValue) query = query.Where(x => x.Branch == _context.Branches
            .Where(b => b.Id == branchId).Select(b => b.Code).FirstOrDefault());
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Location).ToListAsync(cancellationToken);
        var csv = new StringBuilder("SKU,Item,Category,Branch,Location,On Hand,Reserved,Available,Reorder Level\r\n");
        foreach (var x in rows)
            csv.AppendLine(string.Join(',', Cell(x.Sku), Cell(x.Name), Cell(x.Category), Cell(x.Branch),
                Cell(x.Location), Number(x.QuantityOnHand), Number(x.QuantityReserved),
                Number(x.Available), Number(x.ReorderLevel)));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8",
            $"inventory-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    private static (DateTime Start, DateTime End, string? Error) Period(DateTime? from, DateTime? to)
    {
        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        if (start >= end) return (start, end, "The start date must be before the end date.");
        if (end - start > TimeSpan.FromDays(731)) return (start, end, "Report periods cannot exceed two years.");
        return (start, end, null);
    }

    private static string Cell(string value)
    {
        value = value.Trim();
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private async Task<OfficeWorkbook> BuildWorkbook(ReportRequest request, int maximumRowsPerSheet,
        CancellationToken cancellationToken)
    {
        var key = NormalizeReportKey(request.ReportKey);
        var (start, end, error) = Period(request.From, request.To);
        if (error is not null) throw new ArgumentException(error);
        var scope = await _branches.ResolveAsync(false, cancellationToken);
        var branchQuery = _context.Branches.AsNoTracking().Where(x => x.IsActive);
        if (!scope.IsAllBranches)
            branchQuery = branchQuery.Where(x => x.Id == scope.BranchId);
        else if (request.BranchCodes is { Count: > 0 })
        {
            var codes = request.BranchCodes.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToArray();
            branchQuery = branchQuery.Where(x => codes.Contains(x.Code));
        }
        var branches = await branchQuery.OrderBy(x => x.Code).ToListAsync(cancellationToken);
        if (branches.Count == 0) throw new ArgumentException("No accessible branches match the report filters.");
        var sheets = new List<OfficeSheet>();
        foreach (var branch in branches)
        {
            var sheet = key switch
            {
                "sales" => await SalesSheet(branch, start, end, request.Currency, maximumRowsPerSheet,
                    cancellationToken),
                "purchases" => await PurchasesSheet(branch, start, end, request.Currency,
                    maximumRowsPerSheet, cancellationToken),
                "inventory" => await InventorySheet(branch, maximumRowsPerSheet, cancellationToken),
                "schedule" => await ScheduleSheet(branch, start, end, maximumRowsPerSheet,
                    cancellationToken),
                "operations" => await OperationsSheet(branch, start, end, maximumRowsPerSheet,
                    cancellationToken),
                "finance" => await FinanceSheet(branch, start, end, request.Currency,
                    maximumRowsPerSheet, cancellationToken),
                "clients" => await ClientSheet(branch, start, end, maximumRowsPerSheet,
                    cancellationToken),
                "staff" => await StaffSheet(branch, maximumRowsPerSheet, cancellationToken),
                _ => await ExecutiveSheet(branch, start, end, cancellationToken)
            };
            sheets.Add(sheet);
        }
        return new OfficeWorkbook(
            CatalogTitle(key), DateTime.UtcNow, sheets);
    }

    private async Task<OfficeSheet> SalesSheet(Branch branch, DateTime start, DateTime end,
        string? currency, int limit, CancellationToken cancellationToken)
    {
        var query = _context.Invoices.AsNoTracking().Where(x => x.BranchId == branch.Id &&
            x.IssueDate >= start && x.IssueDate <= end && x.Status != InvoiceStatus.Void);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var code = currency.Trim().ToUpperInvariant();
            query = query.Where(x => x.Currency == code);
        }
        var rows = await query.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.Id)
            .Take(limit).ToListAsync(cancellationToken);
        return new OfficeSheet($"{branch.Code} Sales",
            ["Invoice", "Date", "Client", "Type", "Status", "Payment state", "Currency",
                "Total", "Paid", "Credit", "Balance"],
            rows.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.InvoiceNumber), new(x.IssueDate, OfficeCellKind.Date), new(x.CustomerName),
                new(x.Type.ToString()), new(x.Status.ToString()), new(x.PaymentStatus.ToString()),
                new(x.Currency), new(x.TotalAmount, OfficeCellKind.Money),
                new(x.AmountPaid, OfficeCellKind.Money),
                new(x.AppliedCreditAmount, OfficeCellKind.Money),
                new(x.BalanceDue, OfficeCellKind.Money)
            ]).ToList(),
            [
                new("Totals"), new(""), new(""), new(""), new(""), new(""), new(""),
                new(rows.Sum(x => x.TotalAmount), OfficeCellKind.Money),
                new(rows.Sum(x => x.AmountPaid), OfficeCellKind.Money),
                new(rows.Sum(x => x.AppliedCreditAmount), OfficeCellKind.Money),
                new(rows.Sum(x => x.BalanceDue), OfficeCellKind.Money)
            ], $"{branch.Name} · {CountryCurrency.ForCountry(branch.CountryCode)}");
    }

    private async Task<OfficeSheet> PurchasesSheet(Branch branch, DateTime start, DateTime end,
        string? currency, int limit, CancellationToken cancellationToken)
    {
        var query = _context.CompanyPurchases.AsNoTracking().Where(x => x.BranchCode == branch.Code &&
            x.PurchaseDate >= start && x.PurchaseDate <= end);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var code = currency.Trim().ToUpperInvariant();
            query = query.Where(x => x.Currency == code);
        }
        var rows = await query.OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.Id)
            .Take(limit).ToListAsync(cancellationToken);
        return new OfficeSheet($"{branch.Code} Purchases",
            ["Purchase", "Date", "Vendor", "Type", "Category", "Status", "Payment state",
                "Currency", "Total", "Paid", "Balance", "Service or item"],
            rows.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.PurchaseNumber), new(x.PurchaseDate, OfficeCellKind.Date), new(x.VendorName),
                new(x.Type.ToString()), new(x.Category.ToString()), new(x.Status.ToString()),
                new(x.BalanceDue == 0 ? "Paid" : x.AmountPaid > 0 ? "Part paid" : "Unpaid"),
                new(x.Currency), new(x.TotalAmount, OfficeCellKind.Money),
                new(x.AmountPaid, OfficeCellKind.Money),
                new(x.BalanceDue, OfficeCellKind.Money), new(x.Description)
            ]).ToList(),
            [
                new("Totals"), new(""), new(""), new(""), new(""), new(""), new(""), new(""),
                new(rows.Sum(x => x.TotalAmount), OfficeCellKind.Money),
                new(rows.Sum(x => x.AmountPaid), OfficeCellKind.Money),
                new(rows.Sum(x => x.BalanceDue), OfficeCellKind.Money), new("")
            ], $"{branch.Name} · {CountryCurrency.ForCountry(branch.CountryCode)}");
    }

    private async Task<OfficeSheet> InventorySheet(Branch branch, int limit,
        CancellationToken cancellationToken)
    {
        var rows = await (from stock in _context.InventoryStockLevels.AsNoTracking()
            join item in _context.InventoryItems.AsNoTracking() on stock.InventoryItemId equals item.Id
            join location in _context.InventoryLocations.AsNoTracking()
                on stock.InventoryLocationId equals location.Id
            where location.BranchId == branch.Id
            orderby item.Name, location.Name
            select new { item.Sku, item.Name, item.Category, Location = location.Name,
                stock.QuantityOnHand, stock.QuantityReserved,
                Available = stock.QuantityOnHand - stock.QuantityReserved, stock.ReorderLevel })
            .Take(limit).ToListAsync(cancellationToken);
        return new OfficeSheet($"{branch.Code} Inventory",
            ["SKU", "Item", "Category", "Location", "On hand", "Reserved", "Available",
                "Reorder level", "Warning"],
            rows.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.Sku), new(x.Name), new(x.Category), new(x.Location),
                new(x.QuantityOnHand, OfficeCellKind.Number),
                new(x.QuantityReserved, OfficeCellKind.Number),
                new(x.Available, OfficeCellKind.Number),
                new(x.ReorderLevel, OfficeCellKind.Number),
                new(x.Available <= x.ReorderLevel ? "Reorder" : "OK")
            ]).ToList(), Subtitle: branch.Name);
    }

    private async Task<OfficeSheet> ScheduleSheet(Branch branch, DateTime start, DateTime end,
        int limit, CancellationToken cancellationToken)
    {
        var pickups = await _context.ScheduledPickups.AsNoTracking().Where(x =>
                x.BranchId == branch.Id && x.ScheduledAt >= start && x.ScheduledAt <= end &&
                x.DeletedAt == null)
            .OrderBy(x => x.ScheduledAt).Take(limit).ToListAsync(cancellationToken);
        var rows = pickups.Select(x => (IReadOnlyList<OfficeCell>)
        [
            new(x.PickupNumber), new("Pickup"), new(x.ScheduledAt, OfficeCellKind.DateTime),
            new(x.ContactName), new(x.Address), new(x.State.ToString()), new(x.AssignedTeam ?? "")
        ]).ToList();
        var remaining = Math.Max(0, limit - rows.Count);
        if (remaining > 0)
        {
            var appointments = await _context.Appointments.AsNoTracking().Where(x =>
                    x.BranchId == branch.Id && x.StartsAt >= start && x.StartsAt <= end)
                .OrderBy(x => x.StartsAt).Take(remaining).ToListAsync(cancellationToken);
            rows.AddRange(appointments.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.AppointmentNumber), new(x.Type.ToString()),
                new(x.StartsAt, OfficeCellKind.DateTime), new(x.Title), new(x.Location),
                new(x.Status.ToString()), new("")
            ]));
            rows = rows.OrderBy(x => Convert.ToDateTime(x[2].Value)).ToList();
        }
        return new OfficeSheet($"{branch.Code} Schedule",
            ["Reference", "Type", "Date and time", "Person or title", "Location", "Status",
                "Assigned team"], rows, Subtitle: branch.Name);
    }

    private async Task<OfficeSheet> ClientSheet(Branch branch, DateTime start, DateTime end,
        int limit, CancellationToken cancellationToken)
    {
        var rows = await _context.Customers.AsNoTracking().Where(x => x.BranchId == branch.Id &&
                x.CreatedAt >= start && x.CreatedAt <= end)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.FullName).Take(limit)
            .ToListAsync(cancellationToken);
        return new OfficeSheet($"{branch.Code} Clients",
            ["Client number", "Added", "Name", "Phone", "Email", "Hub", "Contact completeness"],
            rows.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.ClientNumber), new(x.CreatedAt, OfficeCellKind.Date), new(x.FullName),
                new(x.Phone), new(x.Email ?? ""), new(branch.Name),
                new(!string.IsNullOrWhiteSpace(x.Phone) && !string.IsNullOrWhiteSpace(x.Email)
                    ? "Phone and email" : !string.IsNullOrWhiteSpace(x.Phone) ? "Phone only"
                    : !string.IsNullOrWhiteSpace(x.Email) ? "Email only" : "Incomplete")
            ]).ToList(), Subtitle: branch.Name);
    }

    private async Task<OfficeSheet> OperationsSheet(Branch branch, DateTime start, DateTime end,
        int limit, CancellationToken cancellationToken)
    {
        var rows = await (from shipment in _context.CustomerShipments.AsNoTracking()
                          join invoice in _context.Invoices.AsNoTracking()
                              on shipment.InvoiceId equals invoice.Id
                          join client in _context.Customers.AsNoTracking()
                              on shipment.CustomerId equals client.Id
                          where shipment.BranchId == branch.Id &&
                                shipment.CreatedAt >= start && shipment.CreatedAt <= end
                          orderby shipment.CreatedAt descending, shipment.Id descending
                          select new
                          {
                              shipment.InvoiceNumber, Client = client.FullName,
                              shipment.Method, shipment.OriginCity,
                              Destination = shipment.DestinationCityOrState,
                              shipment.HandoverMethod, shipment.PieceCount,
                              shipment.ActualWeightKg, shipment.ChargeableWeightKg,
                              shipment.MovementState, invoice.PaymentStatus,
                              invoice.Currency, invoice.TotalAmount, invoice.BalanceDue,
                              shipment.CreatedAt
                          }).Take(limit).ToListAsync(cancellationToken);
        return new OfficeSheet($"{branch.Code} Operations",
            ["Invoice / shipment", "Created", "Client", "Method", "Origin", "Destination",
                "Handover", "Pieces", "Actual kg", "Charged kg", "Movement", "Payment",
                "Currency", "Amount", "Balance"],
            rows.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.InvoiceNumber), new(x.CreatedAt, OfficeCellKind.DateTime), new(x.Client),
                new(x.Method.ToString()), new(x.OriginCity), new(x.Destination),
                new(x.HandoverMethod.ToString()), new(x.PieceCount, OfficeCellKind.Integer),
                new(x.ActualWeightKg ?? 0, OfficeCellKind.Number),
                new(x.ChargeableWeightKg ?? x.ActualWeightKg ?? 0, OfficeCellKind.Number),
                new(x.MovementState.ToString()), new(x.PaymentStatus.ToString()),
                new(x.Currency), new(x.TotalAmount, OfficeCellKind.Money),
                new(x.BalanceDue, OfficeCellKind.Money)
            ]).ToList(), Subtitle: branch.Name);
    }

    private async Task<OfficeSheet> FinanceSheet(Branch branch, DateTime start, DateTime end,
        string? currency, int limit, CancellationToken cancellationToken)
    {
        var query = _context.FinancialTransactions.AsNoTracking().Where(x =>
            x.BranchId == branch.Id && x.OccurredAt >= start && x.OccurredAt <= end);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var code = currency.Trim().ToUpperInvariant();
            query = query.Where(x => x.Currency == code);
        }
        var rows = await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Take(limit).ToListAsync(cancellationToken);
        return new OfficeSheet($"{branch.Code} Finance",
            ["Transaction", "Date", "Direction", "Type", "Freight", "Counterparty",
                "Currency", "Amount", "Payment method", "Account", "Reference", "Status"],
            rows.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.TransactionNumber), new(x.OccurredAt, OfficeCellKind.DateTime),
                new(x.Direction.ToString()), new(x.Type.ToString()),
                new(x.FreightClassification.ToString()), new(x.CounterpartyName),
                new(x.Currency), new(x.Amount, OfficeCellKind.Money), new(x.Method.ToString()),
                new(x.SettlementAccountCode), new(x.ExternalReference), new(x.Status.ToString())
            ]).ToList(), Subtitle: branch.Name);
    }

    private async Task<OfficeSheet> StaffSheet(Branch branch, int limit,
        CancellationToken cancellationToken)
    {
        var rows = await (from profile in _context.StaffProfiles.AsNoTracking()
                          join account in _context.UserAccounts.AsNoTracking()
                              on profile.UserAccountId equals account.Id
                          where profile.BranchCode == branch.Code
                          orderby account.FullName, profile.EmployeeNumber
                          select new
                          {
                              profile.EmployeeNumber, account.FullName, account.Email,
                              account.Role, AccountStatus = account.Status, profile.JobTitle,
                              profile.Department, profile.Status, profile.HireDate,
                              profile.ArchivedAt
                          }).Take(limit).ToListAsync(cancellationToken);
        return new OfficeSheet($"{branch.Code} Staff",
            ["Employee", "Name", "Email", "Role", "Account", "Job title", "Department",
                "Work status", "Hire date", "Archived"],
            rows.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.EmployeeNumber), new(x.FullName), new(x.Email), new(x.Role.ToString()),
                new(x.AccountStatus.ToString()), new(x.JobTitle), new(x.Department),
                new(x.Status.ToString()), new(x.HireDate, OfficeCellKind.Date),
                new(x.ArchivedAt.HasValue ? "Yes" : "No")
            ]).ToList(), Subtitle: branch.Name);
    }

    private async Task<OfficeSheet> ExecutiveSheet(Branch branch, DateTime start, DateTime end,
        CancellationToken cancellationToken)
    {
        var invoices = await _context.Invoices.AsNoTracking().Where(x => x.BranchId == branch.Id &&
            x.IssueDate >= start && x.IssueDate <= end && x.Status != InvoiceStatus.Void)
            .ToListAsync(cancellationToken);
        var purchases = await _context.CompanyPurchases.AsNoTracking().Where(x =>
            x.BranchCode == branch.Code && x.PurchaseDate >= start && x.PurchaseDate <= end &&
            x.Status != CompanyPurchaseStatus.Cancelled && x.Status != CompanyPurchaseStatus.Rejected)
            .ToListAsync(cancellationToken);
        var activeShipments = await _context.CustomerShipments.AsNoTracking().CountAsync(x =>
            x.BranchId == branch.Id && x.MovementState != CustomerShipmentMovementState.Delivered &&
            x.MovementState != CustomerShipmentMovementState.Cancelled, cancellationToken);
        var currency = CountryCurrency.ForCountry(branch.CountryCode);
        IReadOnlyList<IReadOnlyList<OfficeCell>> rows =
        [
            [new("Sales invoices"), new(invoices.Count, OfficeCellKind.Integer), new(currency),
                new(invoices.Sum(x => x.TotalAmount), OfficeCellKind.Money)],
            [new("Payments received"), new(invoices.Count(x => x.AmountPaid > 0), OfficeCellKind.Integer),
                new(currency), new(invoices.Sum(x => x.AmountPaid), OfficeCellKind.Money)],
            [new("Outstanding receivables"), new(invoices.Count(x => x.BalanceDue > 0), OfficeCellKind.Integer),
                new(currency), new(invoices.Sum(x => x.BalanceDue), OfficeCellKind.Money)],
            [new("Purchases"), new(purchases.Count, OfficeCellKind.Integer), new(currency),
                new(purchases.Sum(x => x.TotalAmount), OfficeCellKind.Money)],
            [new("Purchase balance"), new(purchases.Count(x => x.BalanceDue > 0), OfficeCellKind.Integer),
                new(currency), new(purchases.Sum(x => x.BalanceDue), OfficeCellKind.Money)],
            [new("Active customer shipments"), new(activeShipments, OfficeCellKind.Integer), new(""), new("")]
        ];
        return new OfficeSheet($"{branch.Code} Summary",
            ["Metric", "Count", "Currency", "Amount"], rows,
            Subtitle: $"{branch.Name} · {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
    }

    private static string NormalizeReportKey(string value)
    {
        var key = value?.Trim().ToLowerInvariant();
        return key is "executive-summary" or "sales" or "purchases" or "inventory" or
            "schedule" or "operations" or "finance" or "clients" or "staff"
            ? key : throw new ArgumentException("Unknown report key. Use the report catalog.");
    }

    private static string CatalogTitle(string key) => key switch
    {
        "executive-summary" => "BiGSAM Executive Summary",
        "sales" => "BiGSAM Sales and Receivables",
        "purchases" => "BiGSAM Purchases and Payables",
        "inventory" => "BiGSAM Inventory",
        "schedule" => "BiGSAM Schedule",
        "operations" => "BiGSAM Logistics Operations",
        "finance" => "BiGSAM Financial Ledger",
        "clients" => "BiGSAM Client Contacts",
        "staff" => "BiGSAM Staff Access",
        _ => "BiGSAM Report"
    };

    private static ReportExportJobResponse MapJob(ReportExportJob job) => new(job.Id,
        job.ReportKey, job.Status.ToString(), job.FileName, job.CreatedAt, job.CompletedAt,
        job.ExpiresAt, job.ErrorMessage, job.Status == ReportExportStatus.Completed
            ? $"/api/reports/exports/{job.Id}/download" : null);
}
