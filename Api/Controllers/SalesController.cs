using System.Text.Json;
using Application.Common;
using Application.Common.Interfaces;
using Api.Exceptions;
using Api.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Commercial document read model. Creation and accounting remain in the focused
/// operations and finance APIs; this area presents sales without a CRM enquiry pipeline.
/// </summary>
[Authorize(Policy = "SalesTeam")]
[Route("api/sales")]
public sealed class SalesController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    public SalesController(IApplicationDbContext context, IActiveBranchContext branches)
    {
        _context = context;
        _branches = branches;
    }

    [HttpPost("quotations")]
    public async Task<ActionResult> CreateQuotation(CreateSalesQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        if (!Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key))
            throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED",
                "Idempotency key required", "Send a UUID Idempotency-Key header.");
        if (request.InitialStatus is not (QuoteStatus.Draft or QuoteStatus.Sent))
            return UnprocessableEntity(new { Code = "QUOTATION_START_STATE_INVALID",
                Message = "A quotation starts as Draft or Sent." });
        if (string.IsNullOrWhiteSpace(request.Route) || request.ValidUntil.ToUniversalTime() <= DateTime.UtcNow ||
            request.ValidUntil.ToUniversalTime() > DateTime.UtcNow.AddDays(90) ||
            request.Lines.Count == 0 || request.Lines.Any(x =>
                string.IsNullOrWhiteSpace(x.Description) || x.Quantity <= 0 || x.UnitPrice <= 0))
            return UnprocessableEntity(new { Code = "QUOTATION_DETAILS_REQUIRED",
                Message = "Client, route, future validity up to 90 days, and positive quotation lines are required." });
        var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == "sales.quotation.create" &&
            x.Key == key && x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (prior is not null)
            return Ok(await _context.ShippingQuotes.AsNoTracking().SingleAsync(x =>
                x.Id == prior.ResourceId, cancellationToken));
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == request.CustomerId && x.BranchId == branch.BranchId &&
            x.MergedIntoCustomerId == null, cancellationToken);
        if (customer is null)
            throw new ApiProblemException(404, "CLIENT_NOT_FOUND", "Client not found",
                "The selected client is not available in the active branch.");
        var route = await _context.ServiceRoutes.AsNoTracking().Where(x =>
                x.IsActive && x.OriginBranchId == branch.BranchId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (route is null)
            throw new ApiProblemException(422, "SERVICE_ROUTE_REQUIRED",
                "No active service route is configured",
                "Create at least one active service route from the current branch before issuing quotations.");
        var total = request.Lines.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2));
        var lineSnapshot = JsonSerializer.Serialize(new
        {
            Version = 1, Route = request.Route.Trim(), Note = Clean(request.Note),
            Lines = request.Lines.Select(x => new
            {
                Description = x.Description.Trim(), x.Quantity, x.UnitPrice,
                Total = Math.Round(x.Quantity * x.UnitPrice, 2)
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var quote = new ShippingQuote
        {
            QuoteNumber = $"BGL-QT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
            CustomerId = customer.Id,
            ServiceRouteId = route.Id,
            Status = request.InitialStatus,
            ContactName = customer.FullName,
            ContactEmail = customer.Email ?? string.Empty,
            ContactPhone = customer.Phone,
            CargoDescription = lineSnapshot,
            CargoCategory = "Managed quotation",
            ActualWeightKg = 0,
            VolumetricWeightKg = 0,
            BillableWeightKg = 0,
            BaseAmount = total,
            FuelSurchargeAmount = 0,
            CustomsAmount = 0,
            TotalAmount = total,
            Currency = branch.Currency!,
            ExpiresAt = request.ValidUntil.ToUniversalTime()
        };
        _context.ShippingQuotes.Add(quote);
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            ActorUserId = CurrentUserId, Operation = "sales.quotation.create", Key = key,
            ResourceType = nameof(ShippingQuote), ResourceId = quote.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/sales/quotations/{quote.Id}", quote);
    }

    [HttpGet("summary")]
    public async Task<ActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? currency, [FromQuery] string? branchCode,
        CancellationToken cancellationToken)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var (start, end, error) = Period(from, to);
        if (error is not null) return BadRequest(error);
        var selectedCurrency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
        var selectedBranch = activeBranch.IsAllBranches
            ? (string.IsNullOrWhiteSpace(branchCode) ? null : branchCode.Trim().ToUpperInvariant())
            : activeBranch.Code;

        var invoiceQuery = _context.Invoices.AsNoTracking().Where(x =>
            x.IssueDate >= start && x.IssueDate <= end && x.Status != InvoiceStatus.Void);
        if (selectedCurrency is not null) invoiceQuery = invoiceQuery.Where(x => x.Currency == selectedCurrency);
        if (selectedBranch is not null) invoiceQuery = invoiceQuery.Where(x => x.IssuerBranchCode == selectedBranch);
        var invoices = await invoiceQuery.Select(x => new
        {
            x.Type, x.Status, x.PaymentStatus, x.Currency, x.TotalAmount, x.AmountPaid, x.AppliedCreditAmount
        }).ToListAsync(cancellationToken);

        var quoteQuery = _context.ShippingQuotes.AsNoTracking().Where(x =>
            x.CreatedAt >= start && x.CreatedAt <= end);
        if (!activeBranch.IsAllBranches)
        {
            var routeIds = _context.ServiceRoutes.AsNoTracking()
                .Where(x => x.OriginBranchId == activeBranch.BranchId).Select(x => x.Id);
            quoteQuery = quoteQuery.Where(x => routeIds.Contains(x.ServiceRouteId));
        }
        if (selectedCurrency is not null) quoteQuery = quoteQuery.Where(x => x.Currency == selectedCurrency);
        var quotes = await quoteQuery.Select(x => new { x.Status, x.Currency, x.TotalAmount })
            .ToListAsync(cancellationToken);

        var receiptQuery = from document in _context.TransactionDocuments.AsNoTracking()
                           join transaction in _context.FinancialTransactions.AsNoTracking()
                               on document.FinancialTransactionId equals transaction.Id
                           where document.Type == FinancialDocumentType.Receipt &&
                                 document.Status == FinancialDocumentStatus.Issued &&
                                 document.IssuedAt >= start && document.IssuedAt <= end
                           select new { transaction.Currency, transaction.Amount, transaction.Method,
                               transaction.BranchCode };
        if (selectedCurrency is not null) receiptQuery = receiptQuery.Where(x => x.Currency == selectedCurrency);
        if (selectedBranch is not null) receiptQuery = receiptQuery.Where(x => x.BranchCode == selectedBranch);
        var receipts = await receiptQuery.ToListAsync(cancellationToken);

        return Ok(new
        {
            From = start, To = end, Currency = selectedCurrency, BranchCode = selectedBranch,
            InvoicesByCurrency = invoices.GroupBy(x => x.Currency).Select(x => new
            {
                Currency = x.Key, Count = x.Count(y => y.Type == InvoiceType.Sales),
                SalesValue = x.Where(y => y.Type == InvoiceType.Sales).Sum(y => y.TotalAmount),
                CreditNotes = x.Where(y => y.Type == InvoiceType.CreditNote).Sum(y => y.TotalAmount),
                Paid = x.Sum(y => y.AmountPaid),
                Outstanding = x.Where(y => y.Type == InvoiceType.Sales)
                    .Sum(y => Math.Max(0, y.TotalAmount - y.AmountPaid - y.AppliedCreditAmount))
            }),
            QuotationsByCurrency = quotes.GroupBy(x => x.Currency).Select(x => new
            {
                Currency = x.Key, Created = x.Count(),
                Accepted = x.Count(y => y.Status is QuoteStatus.Accepted or QuoteStatus.Converted),
                QuotedValue = x.Sum(y => y.TotalAmount),
                AcceptedValue = x.Where(y => y.Status is QuoteStatus.Accepted or QuoteStatus.Converted)
                    .Sum(y => y.TotalAmount)
            }),
            ReceiptsByCurrency = receipts.GroupBy(x => x.Currency).Select(x => new
                { Currency = x.Key, Count = x.Count(), Amount = x.Sum(y => y.Amount) }),
            ReceiptPaymentsByMethod = receipts.GroupBy(x => new { x.Currency, x.Method }).Select(x => new
                { x.Key.Currency, x.Key.Method, Count = x.Count(), Amount = x.Sum(y => y.Amount) })
        });
    }

    [HttpGet("invoices")]
    public async Task<ActionResult> Invoices([FromQuery] InvoiceType? type,
        [FromQuery] InvoiceStatus? status, [FromQuery] InvoicePaymentStatus? paymentStatus,
        [FromQuery] Guid? customerId, [FromQuery] string? branchCode, [FromQuery] string? currency,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.Invoices.AsNoTracking().Where(x =>
            activeBranch.IsAllBranches || x.IssuerBranchCode == activeBranch.Code);
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (paymentStatus.HasValue) query = query.Where(x => x.PaymentStatus == paymentStatus);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (activeBranch.IsAllBranches && !string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant(); query = query.Where(x => x.IssuerBranchCode == value);
        }
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Currency == value);
        }
        if (from.HasValue) query = query.Where(x => x.IssueDate >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.IssueDate <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.InvoiceNumber.ToLower().Contains(value) ||
                x.CustomerName.ToLower().Contains(value));
        }
        return Ok(await query.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("quotations")]
    public async Task<ActionResult> Quotations([FromQuery] QuoteStatus? status,
        [FromQuery] Guid? customerId, [FromQuery] string? currency, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var routeIds = _context.ServiceRoutes.AsNoTracking().Where(x =>
                branch.IsAllBranches || x.OriginBranchId == branch.BranchId)
            .Select(x => x.Id);
        var query = _context.ShippingQuotes.AsNoTracking().Where(x =>
            routeIds.Contains(x.ServiceRouteId));
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Currency == value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.QuoteNumber.ToLower().Contains(value) ||
                x.ContactName.ToLower().Contains(value) || x.CargoDescription.ToLower().Contains(value));
        }
        return Ok(await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpPatch("quotations/{quotationId:guid}/status")]
    public async Task<ActionResult> ChangeQuotationStatus(Guid quotationId,
        ChangeSalesQuotationStatusRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var routeIds = _context.ServiceRoutes.AsNoTracking().Where(x =>
            x.OriginBranchId == branch.BranchId).Select(x => x.Id);
        var quote = await _context.ShippingQuotes.FirstOrDefaultAsync(x =>
            x.Id == quotationId && routeIds.Contains(x.ServiceRouteId), cancellationToken)
            ?? throw new KeyNotFoundException("Quotation not found.");
        var allowed = quote.Status switch
        {
            QuoteStatus.Draft => request.Status is QuoteStatus.Sent or QuoteStatus.Rejected,
            QuoteStatus.Sent => request.Status is QuoteStatus.Accepted or QuoteStatus.Rejected or QuoteStatus.Expired,
            _ => false
        };
        if (!allowed) return Conflict($"Cannot change a quotation from {quote.Status} to {request.Status}.");
        if (request.Status == QuoteStatus.Rejected && string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("A cancellation reason is required.");
        quote.Status = request.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(quote);
    }

    [HttpGet("receipts")]
    public async Task<ActionResult> Receipts([FromQuery] PaymentMethod? method,
        [FromQuery] string? currency, [FromQuery] string? branchCode,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var activeBranch = await _branches.ResolveAsync(false, cancellationToken);
        var query = from document in _context.TransactionDocuments.AsNoTracking()
                    join transaction in _context.FinancialTransactions.AsNoTracking()
                        on document.FinancialTransactionId equals transaction.Id
                    where document.Type == FinancialDocumentType.Receipt
                    select new SalesReceiptRow
                    {
                        Id = document.Id, DocumentNumber = document.DocumentNumber, Status = document.Status,
                        InvoiceId = document.InvoiceId, PaymentId = document.PaymentId,
                        IssuedAt = document.IssuedAt, VerificationCode = document.VerificationCode,
                        TransactionNumber = transaction.TransactionNumber,
                        CustomerName = transaction.CounterpartyName, Amount = transaction.Amount,
                        Currency = transaction.Currency, Method = transaction.Method,
                        PaymentReference = transaction.ExternalReference, BranchCode = transaction.BranchCode
                    };
        if (!activeBranch.IsAllBranches)
            query = query.Where(x => x.BranchCode == activeBranch.Code);
        if (method.HasValue) query = query.Where(x => x.Method == method);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant(); query = query.Where(x => x.Currency == value);
        }
        if (activeBranch.IsAllBranches && !string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant(); query = query.Where(x => x.BranchCode == value);
        }
        return Ok(await query.OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("legacy-shipping-prices")]
    public async Task<ActionResult> ShippingPrices([FromQuery] string? currency,
        [FromQuery] string? branchCode, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rateQuery = from rate in _context.RateCards.AsNoTracking()
                        join route in _context.ServiceRoutes.AsNoTracking()
                            on rate.ServiceRouteId equals route.Id
                        where rate.IsActive && route.IsActive && rate.EffectiveFrom <= now &&
                              (!rate.EffectiveTo.HasValue || rate.EffectiveTo >= now)
                        select new { rate.Id, RouteId = route.Id, route.Code, Route = route.Name,
                            route.Mode, rate.CargoCategory, rate.MinimumWeightKg, rate.RatePerKg,
                            rate.MinimumCharge, rate.FuelSurchargeRate, rate.CustomsRate, rate.Currency,
                            rate.EffectiveFrom, rate.EffectiveTo };
        var extraQuery = _context.AdditionalServicePrices.AsNoTracking().Where(x => x.IsActive &&
            x.EffectiveFrom <= now && (!x.EffectiveTo.HasValue || x.EffectiveTo >= now));
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var value = currency.Trim().ToUpperInvariant();
            rateQuery = rateQuery.Where(x => x.Currency == value);
            extraQuery = extraQuery.Where(x => x.Currency == value);
        }
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var value = branchCode.Trim().ToUpperInvariant();
            extraQuery = extraQuery.Where(x => x.BranchCode == "GLOBAL" || x.BranchCode == value);
        }
        return Ok(new
        {
            FreightRates = await rateQuery.OrderBy(x => x.Route).ThenBy(x => x.CargoCategory)
                .ToListAsync(cancellationToken),
            AdditionalItemAndServicePrices = await extraQuery.OrderBy(x => x.Category).ThenBy(x => x.Name)
                .ToListAsync(cancellationToken)
        });
    }

    private static (DateTime Start, DateTime End, string? Error) Period(DateTime? from, DateTime? to)
    {
        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        if (start >= end) return (start, end, "The start date must be before the end date.");
        if (end - start > TimeSpan.FromDays(731)) return (start, end, "Sales reports cannot exceed two years.");
        return (start, end, null);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class SalesReceiptRow
    {
        public Guid Id { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public FinancialDocumentStatus Status { get; init; }
        public Guid? InvoiceId { get; init; }
        public Guid? PaymentId { get; init; }
        public DateTime IssuedAt { get; init; }
        public string VerificationCode { get; init; } = string.Empty;
        public string TransactionNumber { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public PaymentMethod Method { get; init; }
        public string PaymentReference { get; init; } = string.Empty;
        public string BranchCode { get; init; } = string.Empty;
    }
}
