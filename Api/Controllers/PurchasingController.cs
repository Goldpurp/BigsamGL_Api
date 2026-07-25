using Application.Common;
using Application.Common.Interfaces;
using Api.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "FinanceTeam")]
[Route("api/purchasing")]
public sealed class PurchasingController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyDocumentNumberGenerator _numbers;
    private readonly IAccountingService _accounting;
    private readonly IFinancialPostingService _postings;
    private readonly IInventoryService _inventory;
    private readonly IActiveBranchContext _branches;

    public PurchasingController(IApplicationDbContext context, ICompanyDocumentNumberGenerator numbers,
        IAccountingService accounting, IFinancialPostingService postings, IInventoryService inventory,
        IActiveBranchContext branches)
    {
        _context = context;
        _numbers = numbers;
        _accounting = accounting;
        _postings = postings;
        _inventory = inventory;
        _branches = branches;
    }

    [HttpPost("purchases")]
    public async Task<ActionResult> Create(CreateCompanyPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var activeBranch = await _branches.ResolveAsync(true, cancellationToken);
        if (!string.Equals(request.BranchCode, activeBranch.Code, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The purchase branch must match the active branch.");
        var validation = await ValidateAsync(request, cancellationToken);
        if (validation.Error is not null) return BadRequest(validation.Error);
        var purchase = new CompanyPurchase
        {
            PurchaseNumber = await _numbers.NextPurchaseAsync(cancellationToken),
            SupplierId = request.SupplierId,
            VendorName = validation.VendorName,
            Type = request.Type ?? InferType(request.Category),
            Category = request.Category,
            BranchCode = validation.BranchCode,
            PurchaseDate = request.PurchaseDate,
            DueDate = request.DueDate,
            Currency = validation.Currency,
            Description = request.Description.Trim(),
            SupplierReference = request.SupplierReference.Trim(),
            RequestedByUserId = CurrentUserId
        };
        var lines = BuildLines(purchase, request.Lines);
        purchase.ReceivingStatus = lines.Any(x => x.InventoryItemId.HasValue)
            ? PurchaseReceivingStatus.Pending : PurchaseReceivingStatus.NotRequired;
        SetTotals(purchase, lines);
        _context.CompanyPurchases.Add(purchase);
        _context.CompanyPurchaseLines.AddRange(lines);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/purchasing/purchases/{purchase.Id}", Map(purchase, lines, Array.Empty<PurchasePayout>()));
    }

    [HttpPut("purchases/{id:guid}")]
    public async Task<ActionResult> Update(Guid id, CreateCompanyPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var activeBranch = await _branches.ResolveAsync(true, cancellationToken);
        if (!string.Equals(request.BranchCode, activeBranch.Code, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The purchase branch must match the active branch.");
        var purchase = await _context.CompanyPurchases.FirstOrDefaultAsync(x =>
            x.Id == id && x.BranchCode == activeBranch.Code,
            cancellationToken) ?? throw new KeyNotFoundException("Company purchase not found.");
        if (purchase.Status != CompanyPurchaseStatus.Draft)
            return Conflict("Only draft purchases can be edited.");
        var validation = await ValidateAsync(request, cancellationToken);
        if (validation.Error is not null) return BadRequest(validation.Error);
        purchase.SupplierId = request.SupplierId;
        purchase.VendorName = validation.VendorName;
        purchase.Type = request.Type ?? InferType(request.Category);
        purchase.Category = request.Category;
        purchase.BranchCode = validation.BranchCode;
        purchase.PurchaseDate = request.PurchaseDate;
        purchase.DueDate = request.DueDate;
        purchase.Currency = validation.Currency;
        purchase.Description = request.Description.Trim();
        purchase.SupplierReference = request.SupplierReference.Trim();
        var currentLines = await _context.CompanyPurchaseLines.Where(x => x.CompanyPurchaseId == id)
            .ToListAsync(cancellationToken);
        _context.CompanyPurchaseLines.RemoveRange(currentLines);
        var replacement = BuildLines(purchase, request.Lines);
        purchase.ReceivingStatus = replacement.Any(x => x.InventoryItemId.HasValue)
            ? PurchaseReceivingStatus.Pending : PurchaseReceivingStatus.NotRequired;
        SetTotals(purchase, replacement);
        _context.CompanyPurchaseLines.AddRange(replacement);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(Map(purchase, replacement, Array.Empty<PurchasePayout>()));
    }

    [HttpPost("purchases/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var activeBranch = await _branches.ResolveAsync(true, cancellationToken);
        var purchase = await _context.CompanyPurchases.FirstOrDefaultAsync(x =>
            x.Id == id && x.BranchCode == activeBranch.Code,
            cancellationToken) ?? throw new KeyNotFoundException("Company purchase not found.");
        if (purchase.Status != CompanyPurchaseStatus.Draft)
            return Conflict("Only draft purchases can be submitted.");
        purchase.Status = CompanyPurchaseStatus.Submitted;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPatch("purchases/{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, DecideCompanyPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Status is not (CompanyPurchaseStatus.Approved or CompanyPurchaseStatus.Rejected))
            return BadRequest("Decision must be Approved or Rejected.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("A decision reason is required.");
        var activeBranch = await _branches.ResolveAsync(true, cancellationToken);
        var purchase = await _context.CompanyPurchases.FirstOrDefaultAsync(x =>
            x.Id == id && x.BranchCode == activeBranch.Code,
            cancellationToken) ?? throw new KeyNotFoundException("Company purchase not found.");
        if (purchase.Status != CompanyPurchaseStatus.Submitted)
            return Conflict("Only submitted purchases can be approved or rejected.");
        purchase.Status = request.Status;
        purchase.DecisionReason = request.Reason.Trim();
        purchase.ApprovedByUserId = CurrentUserId;
        purchase.ApprovedAt = DateTime.UtcNow;
        if (request.Status == CompanyPurchaseStatus.Approved)
        {
            var journalLines = new List<JournalPostingLine>
            {
                new("5000", purchase.Subtotal, 0, purchase.Description),
                new("2000", 0, purchase.TotalAmount, $"Payable to {purchase.VendorName}")
            };
            if (purchase.TaxAmount > 0)
                journalLines.Insert(1, new JournalPostingLine("1200", purchase.TaxAmount, 0,
                    $"Input tax for {purchase.PurchaseNumber}"));
            await _accounting.PrepareJournalAsync("CompanyPurchase", purchase.Id,
                $"Approve purchase {purchase.PurchaseNumber}", purchase.PurchaseDate, purchase.Currency,
                journalLines, cancellationToken, activeBranch.BranchId);
        }
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "FinanceApprover")]
    [HttpPost("purchases/{id:guid}/payouts")]
    public async Task<ActionResult> RecordPayout(Guid id, RecordPurchasePayoutRequest request,
        CancellationToken cancellationToken)
    {
        var activeBranch = await _branches.ResolveAsync(true, cancellationToken);
        var exists = await _context.CompanyPurchases.AsNoTracking().AnyAsync(x =>
            x.Id == id && x.BranchCode == activeBranch.Code, cancellationToken);
        if (!exists) return NotFound("Company purchase not found in the active branch.");
        var result = await _postings.RecordPurchasePayoutAsync(new PurchasePayoutCommand(id,
            request.Reference, request.Amount, request.Currency, request.Method, request.BankAccountId,
            request.PaidAt, request.Notes, CurrentUserId), cancellationToken);
        return Created($"/api/finance/receipts/{result.Document.Id}", new
        {
            result.Payout,
            Transaction = result.Transaction,
            PaymentVoucher = result.Document,
            PrintUrl = $"/api/finance/receipts/{result.Document.Id}/print"
        });
    }

    [HttpGet("purchases")]
    public async Task<ActionResult> GetPurchases([FromQuery] CompanyPurchaseStatus? status,
        [FromQuery] PurchaseReceivingStatus? receivingStatus,
        [FromQuery] CompanyPurchaseType? type, [FromQuery] CompanyPurchaseCategory? category,
        [FromQuery] Guid? supplierId,
        [FromQuery] string? branchCode, [FromQuery] string? currency,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.CompanyPurchases.AsNoTracking().Where(x =>
            activeBranch.IsAllBranches || x.BranchCode == activeBranch.Code);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (receivingStatus.HasValue) query = query.Where(x => x.ReceivingStatus == receivingStatus);
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (category.HasValue) query = query.Where(x => x.Category == category);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId);
        if (activeBranch.IsAllBranches && !string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant(); query = query.Where(x => x.BranchCode == value);
        }
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Currency == value);
        }
        if (from.HasValue) query = query.Where(x => x.PurchaseDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.PurchaseDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.PurchaseNumber.ToLower().Contains(value) ||
                x.VendorName.ToLower().Contains(value) || x.SupplierReference.ToLower().Contains(value) ||
                x.Description.ToLower().Contains(value));
        }
        var ordered = query.OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.Id);
        return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("purchases/{id:guid}")]
    public async Task<ActionResult> GetPurchase(Guid id, CancellationToken cancellationToken)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var purchase = await _context.CompanyPurchases.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && (activeBranch.IsAllBranches || x.BranchCode == activeBranch.Code),
            cancellationToken);
        if (purchase is null) return NotFound();
        var lines = await _context.CompanyPurchaseLines.AsNoTracking().Where(x => x.CompanyPurchaseId == id)
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var payouts = await _context.PurchasePayouts.AsNoTracking().Where(x => x.CompanyPurchaseId == id)
            .OrderByDescending(x => x.PaidAt).ToListAsync(cancellationToken);
        return Ok(Map(purchase, lines, payouts));
    }

    [Authorize(Policy = "InventoryTeam")]
    [HttpPost("purchases/{id:guid}/receive")]
    public async Task<ActionResult> Receive(Guid id, ReceiveCompanyPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var activeBranch = await _branches.ResolveAsync(true, cancellationToken);
        var exists = await _context.CompanyPurchases.AsNoTracking().AnyAsync(x =>
            x.Id == id && x.BranchCode == activeBranch.Code, cancellationToken);
        if (!exists) return NotFound("Company purchase not found in the active branch.");
        var transactions = await _inventory.ReceivePurchaseAsync(new PurchaseReceiptCommand(
            id, request.IdempotencyKey, request.OccurredAt, request.Notes, CurrentUserId,
            request.Lines.Select(x => new PurchaseReceiptLineCommand(
                x.CompanyPurchaseLineId, x.InventoryLocationId, x.Quantity)).ToArray()), cancellationToken);
        return Ok(new { PurchaseId = id, Transactions = transactions });
    }

    [HttpGet("payouts")]
    public async Task<ActionResult> GetPayouts([FromQuery] Guid? purchaseId,
        [FromQuery] PaymentMethod? method, [FromQuery] string? currency,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var scopedPurchases = _context.CompanyPurchases.AsNoTracking().Where(x =>
            activeBranch.IsAllBranches || x.BranchCode == activeBranch.Code).Select(x => x.Id);
        var query = _context.PurchasePayouts.AsNoTracking()
            .Where(x => scopedPurchases.Contains(x.CompanyPurchaseId));
        if (purchaseId.HasValue) query = query.Where(x => x.CompanyPurchaseId == purchaseId);
        if (method.HasValue) query = query.Where(x => x.Method == method);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Currency == value);
        }
        if (from.HasValue) query = query.Where(x => x.PaidAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.PaidAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.PayoutNumber.ToLower().Contains(value) ||
                x.Reference.ToLower().Contains(value));
        }
        return Ok(await query.OrderByDescending(x => x.PaidAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("summary")]
    public async Task<ActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? branchCode, CancellationToken cancellationToken)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        if (start >= end || end - start > TimeSpan.FromDays(731))
            return BadRequest("Choose a valid period of no more than two years.");
        var query = _context.CompanyPurchases.AsNoTracking()
            .Where(x => x.PurchaseDate >= start && x.PurchaseDate <= end &&
                x.Status != CompanyPurchaseStatus.Cancelled && x.Status != CompanyPurchaseStatus.Rejected &&
                (activeBranch.IsAllBranches || x.BranchCode == activeBranch.Code));
        if (activeBranch.IsAllBranches && !string.IsNullOrWhiteSpace(branchCode))
        {
            var code = branchCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.BranchCode == code);
        }
        var purchases = await query.Select(x => new
        {
            x.Id, x.Type, x.Category, x.Currency, x.TotalAmount, x.AmountPaid, x.ReceivingStatus
        }).ToListAsync(cancellationToken);
        var ids = purchases.Select(x => x.Id).ToArray();
        var payments = await _context.PurchasePayouts.AsNoTracking()
            .Where(x => ids.Contains(x.CompanyPurchaseId) && x.Status == PurchasePayoutStatus.Confirmed)
            .GroupBy(x => new { x.Currency, x.Method }).Select(x => new
                { x.Key.Currency, x.Key.Method, Count = x.Count(), Amount = x.Sum(y => y.Amount) })
            .ToListAsync(cancellationToken);
        return Ok(new
        {
            From = start, To = end,
            PurchasesByTypeAndCurrency = purchases.GroupBy(x => new { x.Currency, x.Type })
                .Select(x => new { x.Key.Currency, x.Key.Type, Count = x.Count(),
                    Total = x.Sum(y => y.TotalAmount), Paid = x.Sum(y => y.AmountPaid),
                    Outstanding = x.Sum(y => y.TotalAmount - y.AmountPaid) }),
            GoodsInput = purchases.Where(x => x.Type == CompanyPurchaseType.GoodsInput)
                .GroupBy(x => x.Currency).Select(x => new { Currency = x.Key, Count = x.Count(),
                    Total = x.Sum(y => y.TotalAmount), Received = x.Count(y => y.ReceivingStatus == PurchaseReceivingStatus.Received) }),
            Services = purchases.Where(x => x.Type == CompanyPurchaseType.Service)
                .GroupBy(x => x.Currency).Select(x => new { Currency = x.Key, Count = x.Count(),
                    Total = x.Sum(y => y.TotalAmount) }),
            PaymentsByMethod = payments
        });
    }

    private async Task<(string? Error, string Currency, string BranchCode, string VendorName)> ValidateAsync(
        CreateCompanyPurchaseRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0) return ("A purchase must have at least one line.", "", "", "");
        if (request.DueDate.HasValue && request.DueDate < request.PurchaseDate)
            return ("Due date cannot be before purchase date.", "", "", "");
        var branch = request.BranchCode.Trim().ToUpperInvariant();
        var branchCountry = await _context.Branches.AsNoTracking().Where(x => x.Code == branch && x.IsActive)
            .Select(x => x.CountryCode).FirstOrDefaultAsync(cancellationToken);
        if (branchCountry is null) return ("Active branch was not found.", "", "", "");
        var currency = CountryCurrency.ForCountry(branchCountry, request.Currency.Trim().ToUpperInvariant());
        if (!string.Equals(currency, request.Currency, StringComparison.OrdinalIgnoreCase))
            return ($"{branch} purchases must use {currency}.", "", "", "");
        var vendor = request.VendorName.Trim();
        if (request.SupplierId.HasValue)
        {
            var supplier = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(
                x => x.Id == request.SupplierId && x.IsActive, cancellationToken);
            if (supplier is null) return ("Active supplier was not found.", "", "", "");
            vendor = supplier.Name;
        }
        if (string.IsNullOrWhiteSpace(vendor)) return ("Vendor name is required.", "", "", "");
        var itemIds = request.Lines.Where(x => x.InventoryItemId.HasValue)
            .Select(x => x.InventoryItemId!.Value).Distinct().ToArray();
        if (itemIds.Length > 0)
        {
            var activeItems = await _context.InventoryItems.AsNoTracking()
                .CountAsync(x => itemIds.Contains(x.Id) && x.IsActive, cancellationToken);
            if (activeItems != itemIds.Length)
                return ("One or more active inventory items were not found.", "", "", "");
        }
        return (null, currency, branch, vendor);
    }

    private static List<CompanyPurchaseLine> BuildLines(CompanyPurchase purchase,
        IReadOnlyList<CompanyPurchaseLineRequest> requested) => requested.Select(line =>
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Quantity <= 0 || line.UnitPrice < 0 ||
                line.TaxRate is < 0 or > 1)
                throw new ArgumentException("Purchase line description, quantity, price, and tax rate are invalid.");
            var net = Math.Round(line.Quantity * line.UnitPrice, 2);
            return new CompanyPurchaseLine
            {
                CompanyPurchaseId = purchase.Id,
                InventoryItemId = line.InventoryItemId,
                Description = line.Description.Trim(),
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
                LineTotal = net + Math.Round(net * line.TaxRate, 2)
            };
        }).ToList();

    private static void SetTotals(CompanyPurchase purchase, IReadOnlyCollection<CompanyPurchaseLine> lines)
    {
        purchase.Subtotal = lines.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2));
        purchase.TaxAmount = lines.Sum(x => x.LineTotal - Math.Round(x.Quantity * x.UnitPrice, 2));
        purchase.TotalAmount = purchase.Subtotal + purchase.TaxAmount;
        if (purchase.TotalAmount <= 0) throw new ArgumentException("Purchase total must be greater than zero.");
    }

    private static CompanyPurchaseType InferType(CompanyPurchaseCategory category) => category switch
    {
        CompanyPurchaseCategory.Supplies => CompanyPurchaseType.GoodsInput,
        CompanyPurchaseCategory.Equipment => CompanyPurchaseType.Asset,
        CompanyPurchaseCategory.Maintenance or CompanyPurchaseCategory.ProfessionalService or
            CompanyPurchaseCategory.LogisticsService or CompanyPurchaseCategory.Utilities or
            CompanyPurchaseCategory.Rent or CompanyPurchaseCategory.Regulatory => CompanyPurchaseType.Service,
        _ => CompanyPurchaseType.Other
    };

    private static object Map(CompanyPurchase purchase, IReadOnlyCollection<CompanyPurchaseLine> lines,
        IReadOnlyCollection<PurchasePayout> payouts) => new
    {
        purchase.Id, purchase.PurchaseNumber, purchase.SupplierId, purchase.VendorName, purchase.Type, purchase.Category,
        purchase.Status, purchase.ReceivingStatus, purchase.BranchCode, purchase.PurchaseDate, purchase.DueDate, purchase.Currency,
        purchase.Subtotal, purchase.TaxAmount, purchase.TotalAmount, purchase.AmountPaid, purchase.BalanceDue,
        purchase.Description, purchase.SupplierReference, purchase.RequestedByUserId, purchase.ApprovedByUserId,
        purchase.ApprovedAt, purchase.DecisionReason, Lines = lines, Payouts = payouts
    };
}
