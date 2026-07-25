using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
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

/// <summary>Creates and manages one customer shipment together with its protected invoice.</summary>
[Authorize(Policy = "StaffOnly")]
[Route("api/customer-shipments")]
public sealed class CustomerShipmentsController : ApiControllerBase
{
    private const string OperationName = "customer-shipment.create";
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    private readonly IClientIdentityService _identity;
    private readonly IInvoiceNumberGenerator _invoiceNumbers;
    private readonly ICompanyDocumentNumberGenerator _documentNumbers;
    private readonly IAccountingService _accounting;

    public CustomerShipmentsController(ApplicationDbContext context, IActiveBranchContext branches,
        IClientIdentityService identity, IInvoiceNumberGenerator invoiceNumbers,
        ICompanyDocumentNumberGenerator documentNumbers, IAccountingService accounting)
    {
        _context = context;
        _branches = branches;
        _identity = identity;
        _invoiceNumbers = invoiceNumbers;
        _documentNumbers = documentNumbers;
        _accounting = accounting;
    }

    [HttpPost]
    public async Task<ActionResult<CustomerShipmentResponse>> Create(CreateCustomerShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var idempotencyKey = RequireIdempotencyKey();

        var replay = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == OperationName && x.Key == idempotencyKey &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (replay is not null)
        {
            var replayed = (await LoadResponsesAsync([replay.ResourceId], branch.BranchId,
                cancellationToken)).SingleOrDefault();
            if (replayed is null)
                throw new ApiProblemException(409, "IDEMPOTENCY_SCOPE_MISMATCH",
                    "Idempotency key already used",
                    "Use a new idempotency key for a request in a different branch.");
            Response.Headers.ETag = QuoteVersion(replayed.RowVersion);
            return Ok(replayed);
        }

        var control = await _context.BranchControls.AsNoTracking().FirstOrDefaultAsync(x =>
            x.BranchId == branch.BranchId, cancellationToken);
        if (control is { IsBookingEnabled: false })
            throw new ApiProblemException(409, "BRANCH_BOOKINGS_PAUSED",
                "New bookings are paused",
                control.EmergencyNotice ??
                "The active branch has temporarily paused new shipment bookings.");

        ValidateRequest(request, branch);

        if (!request.Client.ExistingClientId.HasValue)
        {
            var duplicate = await _identity.ValidateNewClientAsync(new ClientIdentityInput(
                request.Client.Name, request.Client.Email, request.Client.Phone, null),
                request.Client.DuplicateReviewId, null, cancellationToken);
            if (duplicate is not null)
                throw DuplicateClient(duplicate);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var lockKey = string.Concat(CurrentUserId.ToString("N"), ":", OperationName, ":", idempotencyKey);
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);

            var prior = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
                x.ActorUserId == CurrentUserId && x.Operation == OperationName && x.Key == idempotencyKey &&
                x.ExpiresAt > DateTime.UtcNow, cancellationToken);
            if (prior is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new CreateResult(prior.ResourceId, false);
            }

            var customer = await ResolveCustomerInsideTransactionAsync(request.Client, branch, cancellationToken);
            if (request.ReferredByClientId == customer.Id)
                throw Rule("SELF_REFERRAL", "A client cannot refer themselves.", "referredByClientId");
            Customer? referrer = null;
            if (request.ReferredByClientId.HasValue)
                referrer = await _context.Customers.FirstOrDefaultAsync(x =>
                    x.Id == request.ReferredByClientId && x.BranchId == branch.BranchId, cancellationToken)
                    ?? throw new ApiProblemException(404, "CLIENT_NOT_FOUND", "Client not found",
                        "The selected referrer is not available in the active branch.");

            var calculated = await CalculateAsync(request, branch, cancellationToken);
            var issuedAt = DateTime.UtcNow;
            var invoiceNumber = await _invoiceNumbers.NextAsync(cancellationToken);
            var invoice = new Invoice
            {
                BranchId = branch.BranchId!.Value,
                CustomerId = customer.Id,
                CustomerName = customer.FullName,
                InvoiceNumber = invoiceNumber,
                Type = InvoiceType.Sales,
                IssuerBranchCode = branch.Code!,
                IssueDate = issuedAt,
                DueDate = issuedAt,
                Status = InvoiceStatus.Issued,
                PaymentStatus = InvoicePaymentStatus.Unpaid,
                PreferredPaymentMethod = request.Payment.Method,
                Currency = calculated.Currency,
                Subtotal = calculated.Subtotal,
                TaxAmount = calculated.Tax,
                TotalAmount = calculated.Total,
                Notes = NullIfWhiteSpace(request.CustomerNote),
                VerificationCode = DocumentSecurity.NewVerificationCode()
            };
            _context.Invoices.Add(invoice);
            foreach (var line in calculated.InvoiceLines)
            {
                line.InvoiceId = invoice.Id;
                _context.InvoiceLines.Add(line);
            }

            var shipment = new CustomerShipment
            {
                BranchId = branch.BranchId.Value,
                CustomerId = customer.Id,
                InvoiceId = invoice.Id,
                InvoiceNumber = invoiceNumber,
                InvoiceNumericFragment = InvoiceDigits(invoiceNumber),
                ReceiverName = PersonName(request.ReceiverName),
                Method = request.Method,
                OriginCountryCode = branch.CountryCode!,
                OriginCity = branch.City!,
                DestinationCountryCode = request.Destination.CountryCode.Trim().ToUpperInvariant(),
                DestinationCityOrState = request.Destination.CityOrState.Trim(),
                DeliveryAddress = request.HandoverMethod == ShipmentHandoverMethod.Delivery
                    ? request.Destination.Address!.Trim() : NullIfWhiteSpace(request.Destination.Address),
                HandoverMethod = request.HandoverMethod,
                GoodsType = request.GoodsType,
                Description = request.Description.Trim(),
                Contents = NullIfWhiteSpace(request.Contents),
                PieceCount = request.PieceCount,
                PricingMethod = request.Pricing.Method,
                ServiceCodeSnapshot = calculated.ServiceCode,
                ActualWeightKg = request.Pricing.ActualWeightKg,
                ChargeableWeightKg = calculated.ChargeableWeightKg ?? request.Pricing.ChargeableWeightKg,
                LengthCm = request.Pricing.LengthCm,
                WidthCm = request.Pricing.WidthCm,
                HeightCm = request.Pricing.HeightCm,
                NegotiatedReason = NullIfWhiteSpace(request.Pricing.NegotiatedReason),
                ReferredByClientId = referrer?.Id,
                CustomerNote = NullIfWhiteSpace(request.CustomerNote)
            };
            _context.CustomerShipments.Add(shipment);
            foreach (var component in calculated.Components)
            {
                component.CustomerShipmentId = shipment.Id;
                _context.CustomerShipmentPriceComponents.Add(component);
            }

            await _accounting.PrepareJournalAsync("Invoice", invoice.Id,
                string.Concat("Post customer shipment ", invoiceNumber), issuedAt, invoice.Currency,
                [
                    new JournalPostingLine("1100", invoice.TotalAmount, 0,
                        string.Concat("Client invoice balance ", invoiceNumber)),
                    new JournalPostingLine("4000", 0, invoice.TotalAmount,
                        string.Concat("Shipping service revenue ", invoiceNumber))
                ], cancellationToken);

            var creditApplied = await ApplyCreditsAsync(invoice, request.CreditApplications ?? [],
                branch.BranchId.Value, idempotencyKey, shipment.Id, issuedAt, cancellationToken);
            invoice.AppliedCreditAmount = creditApplied;
            ValidatePayment(request.Payment, invoice.TotalAmount - creditApplied);
            if (request.Payment.AmountPaid > 0)
                await AddPaymentAsync(invoice, request.Payment, branch, request.Method, issuedAt,
                    cancellationToken);
            ApplyPaymentState(invoice, request.Payment.AmountPaid);

            if (referrer is not null)
                await AwardReferralAsync(referrer, customer, invoice, cancellationToken);

            _context.IdempotencyRecords.Add(new IdempotencyRecord
            {
                ActorUserId = CurrentUserId,
                Operation = OperationName,
                Key = idempotencyKey,
                ResourceType = nameof(CustomerShipment),
                ResourceId = shipment.Id,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CreateResult(shipment.Id, true);
        });

        var response = (await LoadResponsesAsync([result.ShipmentId], branch.BranchId,
            cancellationToken)).Single();
        Response.Headers.ETag = QuoteVersion(response.RowVersion);
        return result.Created
            ? CreatedAtAction(nameof(Get), new { invoiceNumber = response.InvoiceNumber }, response)
            : Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search,
        [FromQuery] ShipmentPaymentState? paymentState,
        [FromQuery] CustomerShipmentMovementState? movementState,
        [FromQuery] CustomerShipmentMethod? method, [FromQuery] DateTime? from,
        [FromQuery] DateTime? to, [FromQuery] string? destination,
        [FromQuery] Guid? manifestId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = from shipment in _context.CustomerShipments.AsNoTracking()
                    join invoice in _context.Invoices.AsNoTracking() on shipment.InvoiceId equals invoice.Id
                    join client in _context.Customers.AsNoTracking() on shipment.CustomerId equals client.Id
                    where branch.IsAllBranches || shipment.BranchId == branch.BranchId
                    select new { Shipment = shipment, Invoice = invoice, Client = client };
        if (method.HasValue) query = query.Where(x => x.Shipment.Method == method);
        if (movementState.HasValue) query = query.Where(x => x.Shipment.MovementState == movementState);
        if (paymentState.HasValue)
        {
            var invoiceState = paymentState switch
            {
                ShipmentPaymentState.Paid => InvoicePaymentStatus.Paid,
                ShipmentPaymentState.Partial => InvoicePaymentStatus.PartiallyPaid,
                _ => InvoicePaymentStatus.Unpaid
            };
            query = query.Where(x => x.Invoice.PaymentStatus == invoiceState);
        }
        if (from.HasValue) query = query.Where(x => x.Shipment.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.Shipment.CreatedAt < to.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrWhiteSpace(destination))
        {
            var value = destination.Trim().ToLowerInvariant();
            query = query.Where(x => x.Shipment.DestinationCountryCode.ToLower().Contains(value) ||
                x.Shipment.DestinationCityOrState.ToLower().Contains(value));
        }
        if (manifestId.HasValue)
            query = query.Where(x => _context.OperationalManifestEntries.Any(line =>
                line.ManifestId == manifestId && line.InvoiceId == x.Invoice.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Shipment.InvoiceNumber.ToLower().Contains(value) ||
                x.Client.FullName.ToLower().Contains(value) || x.Client.Phone.Contains(value) ||
                (x.Client.Email != null && x.Client.Email.ToLower().Contains(value)) ||
                x.Shipment.ReceiverName.ToLower().Contains(value) ||
                x.Shipment.Description.ToLower().Contains(value));
        }

        var paged = await query.OrderByDescending(x => x.Shipment.CreatedAt)
            .ThenByDescending(x => x.Shipment.Id).Select(x => x.Shipment.Id)
            .ToPagedResponseAsync(page, pageSize, true, cancellationToken);
        var responses = await LoadResponsesAsync(paged.Items, branch.IsAllBranches ? null : branch.BranchId,
            cancellationToken);
        return Ok(new PagedResponse<CustomerShipmentResponse>(responses, paged.Page, paged.PageSize,
            paged.HasPrevious, paged.HasNext, paged.Total, paged.TotalPages));
    }

    [HttpGet("summary")]
    public async Task<ActionResult> Summary([FromQuery] ShipmentPaymentState? paymentState,
        [FromQuery] CustomerShipmentMovementState? movementState,
        [FromQuery] CustomerShipmentMethod? method, [FromQuery] DateTime? from,
        [FromQuery] DateTime? to, [FromQuery] string? destination,
        [FromQuery] Guid? manifestId, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = from shipment in _context.CustomerShipments.AsNoTracking()
                    join invoice in _context.Invoices.AsNoTracking() on shipment.InvoiceId equals invoice.Id
                    where branch.IsAllBranches || shipment.BranchId == branch.BranchId
                    select new { ShipmentId = shipment.Id, shipment.Method, shipment.MovementState,
                        shipment.DestinationCountryCode, shipment.DestinationCityOrState,
                        shipment.CreatedAt, InvoiceId = invoice.Id, invoice.Currency,
                        invoice.TotalAmount, invoice.AmountPaid, invoice.AppliedCreditAmount,
                        invoice.PaymentStatus };
        if (method.HasValue) query = query.Where(x => x.Method == method);
        if (movementState.HasValue) query = query.Where(x => x.MovementState == movementState);
        if (paymentState.HasValue)
        {
            var state = paymentState switch
            {
                ShipmentPaymentState.Paid => InvoicePaymentStatus.Paid,
                ShipmentPaymentState.Partial => InvoicePaymentStatus.PartiallyPaid,
                _ => InvoicePaymentStatus.Unpaid
            };
            query = query.Where(x => x.PaymentStatus == state);
        }
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrWhiteSpace(destination))
        {
            var value = destination.Trim().ToLowerInvariant();
            query = query.Where(x => x.DestinationCountryCode.ToLower().Contains(value) ||
                x.DestinationCityOrState.ToLower().Contains(value));
        }
        if (manifestId.HasValue)
            query = query.Where(x => _context.OperationalManifestEntries.Any(line =>
                line.ManifestId == manifestId && line.InvoiceId == x.InvoiceId));
        var rows = await query.ToListAsync(cancellationToken);
        return Ok(new
        {
            Total = rows.Count,
            Air = rows.Count(x => x.Method == CustomerShipmentMethod.Air),
            Sea = rows.Count(x => x.Method == CustomerShipmentMethod.Sea),
            New = rows.Count(x => x.MovementState == CustomerShipmentMovementState.New),
            ByCurrency = rows.GroupBy(x => x.Currency).OrderBy(x => x.Key).Select(x => new
            {
                Currency = x.Key, Billed = x.Sum(y => y.TotalAmount), Paid = x.Sum(y => y.AmountPaid),
                CreditApplied = x.Sum(y => y.AppliedCreditAmount),
                Balance = x.Sum(y => Math.Max(0, y.TotalAmount - y.AmountPaid - y.AppliedCreditAmount)),
                PaidCount = x.Count(y => y.PaymentStatus == InvoicePaymentStatus.Paid),
                PartialCount = x.Count(y => y.PaymentStatus == InvoicePaymentStatus.PartiallyPaid),
                UnpaidCount = x.Count(y => y.PaymentStatus == InvoicePaymentStatus.Unpaid)
            })
        });
    }

    [HttpGet("recent-updates")]
    public async Task<ActionResult> RecentUpdates([FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        take = Math.Clamp(take, 1, 100);
        var rows = await (from shipment in _context.CustomerShipments.AsNoTracking()
            join client in _context.Customers.AsNoTracking() on shipment.CustomerId equals client.Id
            join invoice in _context.Invoices.AsNoTracking() on shipment.InvoiceId equals invoice.Id
            where branch.IsAllBranches || shipment.BranchId == branch.BranchId
            orderby (shipment.LastModifiedAt ?? shipment.CreatedAt) descending, shipment.Id descending
            select new
            {
                shipment.InvoiceNumber, ClientName = client.FullName, shipment.Method,
                shipment.MovementState, invoice.PaymentStatus,
                UpdatedAt = shipment.LastModifiedAt ?? shipment.CreatedAt,
                shipment.DestinationCountryCode, shipment.DestinationCityOrState
            }).Take(take).ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpGet("manifest-warnings")]
    public async Task<ActionResult> ManifestWarnings([FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        take = Math.Clamp(take, 1, 250);
        var cutoff = DateTime.UtcNow.AddDays(-3);
        var rows = await (from shipment in _context.CustomerShipments.AsNoTracking()
            join client in _context.Customers.AsNoTracking() on shipment.CustomerId equals client.Id
            where (branch.IsAllBranches || shipment.BranchId == branch.BranchId) &&
                  shipment.MovementState != CustomerShipmentMovementState.Cancelled &&
                  shipment.MovementState != CustomerShipmentMovementState.Delivered &&
                  !_context.OperationalManifestEntries.Any(line => line.InvoiceId == shipment.InvoiceId)
            orderby shipment.CreatedAt
            select new
            {
                shipment.InvoiceNumber, ClientName = client.FullName, shipment.Method,
                shipment.CreatedAt,
                Warning = shipment.CreatedAt < cutoff
                    ? "Shipment has waited more than three days without a manifest."
                    : "Shipment is not assigned to a manifest."
            }).Take(take).ToListAsync(cancellationToken);
        return Ok(new { Total = rows.Count, Items = rows });
    }

    [HttpGet("{invoiceNumber}")]
    public async Task<ActionResult<CustomerShipmentResponse>> Get(string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var normalized = ClientIdentityNormalizer.Invoice(invoiceNumber);
        var id = await _context.CustomerShipments.AsNoTracking().Where(x =>
                x.InvoiceNumber == normalized && (branch.IsAllBranches || x.BranchId == branch.BranchId))
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (!id.HasValue) return NotFound();
        var response = (await LoadResponsesAsync([id.Value], branch.IsAllBranches ? null : branch.BranchId,
            cancellationToken)).Single();
        Response.Headers.ETag = QuoteVersion(response.RowVersion);
        return Ok(response);
    }

    [HttpPatch("{invoiceNumber}")]
    public async Task<ActionResult<CustomerShipmentResponse>> Update(string invoiceNumber,
        UpdateCustomerShipmentRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var expected = RequireVersion();
        ValidateUpdate(request, branch);
        var normalized = ClientIdentityNormalizer.Invoice(invoiceNumber);
        var shipment = await _context.CustomerShipments.FirstOrDefaultAsync(x =>
            x.InvoiceNumber == normalized && x.BranchId == branch.BranchId, cancellationToken);
        if (shipment is null) return NotFound();
        if (shipment.RowVersion != expected)
            throw new ApiProblemException(412, "PRECONDITION_FAILED", "The shipment changed",
                "Reload the current shipment before applying this edit.", extensions:
                new Dictionary<string, object?> { ["currentRowVersion"] = shipment.RowVersion });
        if (shipment.MovementState == CustomerShipmentMovementState.Cancelled)
            throw Rule("SHIPMENT_CANCELLED", "A cancelled shipment cannot be edited.");
        shipment.ReceiverName = PersonName(request.ReceiverName);
        shipment.DestinationCountryCode = request.Destination.CountryCode.Trim().ToUpperInvariant();
        shipment.DestinationCityOrState = request.Destination.CityOrState.Trim();
        shipment.DeliveryAddress = request.HandoverMethod == ShipmentHandoverMethod.Delivery
            ? request.Destination.Address!.Trim() : NullIfWhiteSpace(request.Destination.Address);
        shipment.HandoverMethod = request.HandoverMethod;
        shipment.GoodsType = request.GoodsType;
        shipment.Description = request.Description.Trim();
        shipment.Contents = NullIfWhiteSpace(request.Contents);
        shipment.PieceCount = request.PieceCount;
        shipment.CustomerNote = NullIfWhiteSpace(request.CustomerNote);
        shipment.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        var response = (await LoadResponsesAsync([shipment.Id], branch.BranchId, cancellationToken)).Single();
        Response.Headers.ETag = QuoteVersion(response.RowVersion);
        return Ok(response);
    }

    [HttpPost("{invoiceNumber}/cancel")]
    public async Task<ActionResult<CustomerShipmentResponse>> Cancel(string invoiceNumber,
        [FromBody] CancelCustomerShipmentRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var expected = RequireVersion();
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("CANCELLATION_REASON_REQUIRED", "A cancellation reason is required.", "reason");
        var normalized = ClientIdentityNormalizer.Invoice(invoiceNumber);
        var shipment = await _context.CustomerShipments.FirstOrDefaultAsync(x =>
            x.InvoiceNumber == normalized && x.BranchId == branch.BranchId, cancellationToken);
        if (shipment is null) return NotFound();
        if (shipment.RowVersion != expected)
            throw new ApiProblemException(412, "PRECONDITION_FAILED", "The shipment changed",
                "Reload the current shipment before cancelling it.");
        if (shipment.MovementState is CustomerShipmentMovementState.OnTheWay or CustomerShipmentMovementState.Delivered)
            throw Rule("SHIPMENT_CANNOT_BE_CANCELLED",
                "A shipment already on the way or delivered cannot be cancelled through this action.");
        shipment.MovementState = CustomerShipmentMovementState.Cancelled;
        shipment.CancelledAt = DateTime.UtcNow;
        shipment.CancelledByUserId = CurrentUserId;
        shipment.CancellationReason = request.Reason.Trim();
        shipment.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok((await LoadResponsesAsync([shipment.Id], branch.BranchId, cancellationToken)).Single());
    }

    [HttpGet("{invoiceNumber}/timeline")]
    public async Task<ActionResult> Timeline(string invoiceNumber, [FromQuery] string? cursor,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var shipment = await _context.CustomerShipments.AsNoTracking().FirstOrDefaultAsync(x =>
            x.InvoiceNumber == invoiceNumber.ToUpper() && (branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken);
        if (shipment is null) return NotFound();
        pageSize = new[] { 10, 25, 50, 100 }.Contains(pageSize) ? pageSize : 25;
        var query = _context.AuditLogs.AsNoTracking().Where(x => x.EntityId == shipment.Id.ToString() &&
            x.EntityType == nameof(CustomerShipment));
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var position = PaginationExtensions.DecodeCursor(cursor);
            query = query.Where(x => x.CreatedAt < position.Timestamp ||
                (x.CreatedAt == position.Timestamp && x.Id.CompareTo(position.Id) < 0));
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0
            ? PaginationExtensions.EncodeCursor(rows[^1].CreatedAt, rows[^1].Id) : null;
        return Ok(new CursorPagedResponse<AuditLog>(rows, pageSize, hasMore, next));
    }

    private async Task<Customer> ResolveCustomerInsideTransactionAsync(CustomerShipmentClientInput input,
        ActiveBranchScope branch, CancellationToken cancellationToken)
    {
        if (input.ExistingClientId.HasValue)
            return await _context.Customers.FirstOrDefaultAsync(x => x.Id == input.ExistingClientId &&
                x.BranchId == branch.BranchId, cancellationToken)
                ?? throw new ApiProblemException(404, "CLIENT_NOT_FOUND", "Client not found",
                    "The selected client is not available in the active branch.");

        var name = ClientIdentityNormalizer.Name(input.Name);
        var phone = ClientIdentityNormalizer.Phone(input.Phone)!;
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : ClientIdentityNormalizer.Email(input.Email);
        var exact = await _context.Customers.AsNoTracking().Where(x => x.NormalizedFullName == name ||
                x.NormalizedPhone == phone || (email != null && x.NormalizedEmail == email))
            .OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.ClientNumber, x.FullName, x.Phone, x.Email }).Take(10)
            .ToListAsync(cancellationToken);
        if (exact.Count > 0)
            throw new ApiProblemException(409, "CLIENT_MATCH_FOUND",
                "These client details match an existing record",
                "Choose the existing client before starting the shipment.",
                extensions: new Dictionary<string, object?> { ["matches"] = exact });

        var customer = new Customer(Guid.NewGuid(), PersonName(input.Name),
            string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant(), 0,
            branch.CountryCode!)
        {
            ClientNumber = await _documentNumbers.NextClientAsync(cancellationToken),
            BranchId = branch.BranchId!.Value,
            Phone = input.Phone.Trim(),
            WalletCurrency = branch.Currency!
        };
        _context.Customers.Add(customer);
        _context.NotificationPreferences.Add(new NotificationPreference { CustomerId = customer.Id });
        return customer;
    }

    private async Task<PriceCalculation> CalculateAsync(CreateCustomerShipmentRequest request,
        ActiveBranchScope branch, CancellationToken cancellationToken)
    {
        var components = new List<CustomerShipmentPriceComponent>();
        var lines = new List<InvoiceLine>();
        var serviceCode = NullIfWhiteSpace(request.Pricing.ServiceCode)?.ToUpperInvariant();

        if (serviceCode is not null && request.Pricing.Method != CustomerShipmentPricingMethod.NegotiatedPrice)
        {
            var expectedMethod = request.Method == CustomerShipmentMethod.Air
                ? ShippingPriceServiceMethod.Air : ShippingPriceServiceMethod.Sea;
            var prices = await _context.ShippingPriceCatalogueItems.AsNoTracking()
                .Where(x => x.BranchId == branch.BranchId && x.ServiceCode == serviceCode &&
                    x.State == ShippingPriceState.Active && x.Method == expectedMethod)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);
            if (prices.Count == 0)
                throw Rule("MANAGED_SERVICE_NOT_AVAILABLE",
                    "The selected service is inactive or is not available from the active hub.",
                    "pricing.serviceCode");
            if (prices.Select(x => x.Currency).Distinct().Count() != 1)
                throw Rule("SERVICE_CURRENCY_MISMATCH",
                    "All active prices in a service must use one currency. Correct the pricing catalogue first.",
                    "pricing.serviceCode");

            var allowedDestinations = prices.SelectMany(x => x.DestinationCountryCodes.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(x => x.ToUpperInvariant()).Distinct().ToHashSet();
            var destination = request.Destination.CountryCode.Trim().ToUpperInvariant();
            if (allowedDestinations.Count > 0 && !allowedDestinations.Contains(destination))
                throw Rule("DESTINATION_NOT_IN_SERVICE",
                    "The selected destination is not covered by this managed service.",
                    "destination.countryCode");

            var dimensions = new[] { request.Pricing.LengthCm, request.Pricing.WidthCm, request.Pricing.HeightCm };
            var dimensionCount = dimensions.Count(x => x.HasValue && x > 0);
            if (dimensionCount is > 0 and < 3)
                throw Rule("INCOMPLETE_DIMENSIONS",
                    "Length, width and height must all be supplied when dimensions are used.", "pricing");
            var hasDimensions = dimensionCount == 3;
            var volumeCbm = hasDimensions
                ? request.Pricing.LengthCm!.Value * request.Pricing.WidthCm!.Value *
                  request.Pricing.HeightCm!.Value / 1_000_000m
                : (decimal?)null;
            var volumetricWeight = hasDimensions
                ? request.Pricing.LengthCm!.Value * request.Pricing.WidthCm!.Value *
                  request.Pricing.HeightCm!.Value / 5_000m
                : (decimal?)null;
            var basePrices = prices.Where(x =>
                x.Group.Equals("Base rate", StringComparison.OrdinalIgnoreCase)).ToList();
            var selections = request.Pricing.BoxItems ?? [];
            var usesCbm = basePrices.Any(x => x.Unit.Equals("cbm", StringComparison.OrdinalIgnoreCase));
            decimal billableQuantity;
            decimal? chargeableWeight = null;
            if (usesCbm)
            {
                billableQuantity = volumeCbm is > 0 ? decimal.Round(volumeCbm.Value, 4) : 0;
            }
            else if (basePrices.Count > 0)
            {
                if (!request.Pricing.ActualWeightKg.HasValue || request.Pricing.ActualWeightKg <= 0)
                    throw Rule("ACTUAL_WEIGHT_REQUIRED",
                        "Enter the actual weight for this managed service.", "pricing.actualWeightKg");
                chargeableWeight = Math.Max(request.Pricing.ActualWeightKg.Value,
                    Math.Max(volumetricWeight ?? 0, request.Pricing.ChargeableWeightKg ?? 0));
                billableQuantity = chargeableWeight.Value;
            }
            else
            {
                billableQuantity = 0;
            }

            var selectedBase = billableQuantity > 0
                ? basePrices.FirstOrDefault(x =>
                    billableQuantity >= (x.MinimumQuantity ?? 0) &&
                    (!x.MaximumQuantity.HasValue || billableQuantity <= x.MaximumQuantity.Value))
                : null;
            if (selectedBase is null && basePrices.Count > 0)
            {
                var firstBand = basePrices.OrderBy(x => x.MinimumQuantity ?? 0).First();
                if (billableQuantity > 0 && billableQuantity < (firstBand.MinimumQuantity ?? 0))
                    selectedBase = firstBand;
            }
            if (basePrices.Count > 0 && selectedBase is null && selections.Count == 0)
                throw Rule("NO_PRICE_BAND",
                    usesCbm
                        ? "Select a Sea box or item, or enter complete dimensions only when CBM pricing is needed."
                        : "No active price band covers this shipment quantity.", "pricing");

            var index = 0;
            if (selectedBase is not null)
            {
                var chargedQuantity = Math.Max(billableQuantity, selectedBase.MinimumQuantity ?? 0);
                var amount = CatalogueAmount(selectedBase, chargedQuantity);
                AddCatalogueLine(selectedBase, chargedQuantity, amount,
                    ShipmentPriceComponentType.Weight, index++);
                billableQuantity = chargedQuantity;
                if (!usesCbm) chargeableWeight = chargedQuantity;
            }

            if (selections.Any(x => x.Quantity <= 0) ||
                selections.Select(x => x.CataloguePriceId).Distinct().Count() != selections.Count)
                throw Rule("INVALID_CATALOGUE_SELECTION",
                    "Select each service price once with a positive quantity.", "pricing.boxItems");
            foreach (var selection in selections)
            {
                var price = prices.FirstOrDefault(x => x.Id == selection.CataloguePriceId &&
                    !x.Group.Equals("Base rate", StringComparison.OrdinalIgnoreCase))
                    ?? throw Rule("CATALOGUE_PRICE_NOT_AVAILABLE",
                        "A selected item is not part of the chosen service.", "pricing.boxItems");
                var amount = CatalogueAmount(price, selection.Quantity);
                AddCatalogueLine(price, selection.Quantity, amount,
                    price.Unit.Equals("box", StringComparison.OrdinalIgnoreCase)
                        ? ShipmentPriceComponentType.CatalogueBox
                        : ShipmentPriceComponentType.CatalogueItem, index++);
            }
            if (selectedBase is null && selections.Count == 0)
                throw Rule("PRICE_SELECTION_REQUIRED",
                    "Select at least one box, item, pallet or vehicle.", "pricing.boxItems");

            if (selectedBase?.HandlingFee is > 0)
                AddFee("HANDLING", "Handling fee", selectedBase.HandlingFee.Value, index++);
            if (selectedBase?.DocumentationFee is > 0)
                AddFee("DOCUMENTATION", "Documentation fee", selectedBase.DocumentationFee.Value, index++);

            var managedSubtotal = lines.Sum(x => x.LineTotal);
            if (managedSubtotal <= 0)
                throw Rule("INVALID_INVOICE_TOTAL", "The shipment total must be greater than zero.");
            return new PriceCalculation(components, lines, managedSubtotal, 0, managedSubtotal,
                prices[0].Currency, serviceCode, chargeableWeight);

            decimal CatalogueAmount(ShippingPriceCatalogueItem price, decimal quantity)
            {
                if (price.PriceKind == ShippingPriceKind.CustomQuote || !price.LowerPrice.HasValue)
                    throw Rule("NEGOTIATED_PRICE_REQUIRED",
                        $"{price.Name} requires a negotiated shipment with an approval note.",
                        "pricing.method");
                var quantityUnit = price.Unit.Equals("kg", StringComparison.OrdinalIgnoreCase) ||
                    price.Unit.Equals("cbm", StringComparison.OrdinalIgnoreCase) ||
                    price.Unit.Equals("box", StringComparison.OrdinalIgnoreCase) ||
                    price.Unit.Equals("item", StringComparison.OrdinalIgnoreCase) ||
                    price.Unit.Equals("pallet", StringComparison.OrdinalIgnoreCase) ||
                    price.Unit.Equals("vehicle", StringComparison.OrdinalIgnoreCase) ||
                    price.Unit.Equals("surcharge", StringComparison.OrdinalIgnoreCase);
                var amount = quantityUnit ? price.LowerPrice.Value * quantity : price.LowerPrice.Value;
                if (price.MinimumCharge.HasValue) amount = Math.Max(amount, price.MinimumCharge.Value);
                return Money(amount);
            }

            void AddCatalogueLine(ShippingPriceCatalogueItem price, decimal quantity, decimal amount,
                ShipmentPriceComponentType type, int sortOrder)
            {
                lines.Add(new InvoiceLine
                {
                    Description = price.Name, Quantity = quantity, Unit = price.Unit,
                    UnitPrice = price.LowerPrice!.Value, TaxRate = 0, LineTotal = amount
                });
                components.Add(new CustomerShipmentPriceComponent
                {
                    CataloguePriceId = price.Id, Type = type, CodeSnapshot = price.Code,
                    DescriptionSnapshot = price.Name, UnitSnapshot = price.Unit,
                    Quantity = quantity, UnitPrice = price.LowerPrice.Value,
                    LineAmount = amount, SortOrder = sortOrder
                });
            }

            void AddFee(string code, string description, decimal amount, int sortOrder)
            {
                var rounded = Money(amount);
                lines.Add(new InvoiceLine
                {
                    Description = description, Quantity = 1, Unit = "service",
                    UnitPrice = rounded, TaxRate = 0, LineTotal = rounded
                });
                components.Add(new CustomerShipmentPriceComponent
                {
                    Type = ShipmentPriceComponentType.CatalogueItem, CodeSnapshot = code,
                    DescriptionSnapshot = description, UnitSnapshot = "service",
                    Quantity = 1, UnitPrice = rounded, LineAmount = rounded, SortOrder = sortOrder
                });
            }
        }

        if (request.Pricing.Method == CustomerShipmentPricingMethod.Weight)
        {
            if (request.Method != CustomerShipmentMethod.Air)
                throw Rule("PRICING_METHOD_MISMATCH", "Weight pricing is available only for Air shipments.", "pricing.method");
            if (!request.Pricing.ActualWeightKg.HasValue || !request.Pricing.ChargeableWeightKg.HasValue ||
                request.Pricing.ActualWeightKg <= 0 ||
                request.Pricing.ChargeableWeightKg < request.Pricing.ActualWeightKg)
                throw Rule("INVALID_AIR_WEIGHT",
                    "Air chargeable weight must be provided and cannot be below actual weight.", "pricing.chargeableWeightKg");
            if (request.InvoiceLines.Count == 0)
                throw Rule("INVOICE_LINES_REQUIRED", "Add at least one Air freight invoice line.", "invoiceLines");
            var index = 0;
            foreach (var input in request.InvoiceLines)
            {
                ValidateInvoiceLine(input);
                var baseAmount = Money(input.Quantity * input.UnitPrice);
                var lineTax = Money(baseAmount * input.TaxRate / 100m);
                lines.Add(new InvoiceLine { Description = input.Description.Trim(), Quantity = input.Quantity,
                    Unit = input.Unit.Trim(), UnitPrice = input.UnitPrice, TaxRate = input.TaxRate,
                    LineTotal = baseAmount + lineTax });
                components.Add(new CustomerShipmentPriceComponent
                {
                    Type = ShipmentPriceComponentType.Weight, CodeSnapshot = "AIR-WEIGHT",
                    DescriptionSnapshot = input.Description.Trim(), UnitSnapshot = input.Unit.Trim(),
                    Quantity = input.Quantity, UnitPrice = input.UnitPrice,
                    LineAmount = baseAmount + lineTax, SortOrder = index++
                });
            }
        }
        else if (request.Pricing.Method == CustomerShipmentPricingMethod.BoxSizesAndItems)
        {
            if (request.Method != CustomerShipmentMethod.Sea)
                throw Rule("PRICING_METHOD_MISMATCH",
                    "Box-size and item pricing is available only for Sea shipments.", "pricing.method");
            if (request.Pricing.BoxItems.Count == 0 || request.Pricing.BoxItems.Any(x => x.Quantity <= 0))
                throw Rule("SEA_ITEMS_REQUIRED", "Select at least one approved Sea box or item.", "pricing.boxItems");
            var ids = request.Pricing.BoxItems.Select(x => x.CataloguePriceId).Distinct().ToList();
            var prices = await _context.ShippingPriceCatalogueItems.AsNoTracking().Where(x =>
                ids.Contains(x.Id) && x.BranchId == branch.BranchId && x.State == ShippingPriceState.Active &&
                x.Method == ShippingPriceServiceMethod.Sea).ToDictionaryAsync(x => x.Id, cancellationToken);
            if (prices.Count != ids.Count)
                throw Rule("CATALOGUE_PRICE_NOT_AVAILABLE",
                    "One or more selected Sea prices are inactive or outside the active branch.", "pricing.boxItems");
            var index = 0;
            foreach (var selection in request.Pricing.BoxItems)
            {
                var price = prices[selection.CataloguePriceId];
                if (price.PriceKind == ShippingPriceKind.CustomQuote || !price.LowerPrice.HasValue)
                    throw Rule("NEGOTIATED_PRICE_REQUIRED",
                        "Custom-quote catalogue items must use NegotiatedPrice.", "pricing.method");
                var amount = Money(selection.Quantity * price.LowerPrice.Value);
                lines.Add(new InvoiceLine { Description = price.Name, Quantity = selection.Quantity,
                    Unit = price.Unit, UnitPrice = price.LowerPrice.Value, TaxRate = 0, LineTotal = amount });
                components.Add(new CustomerShipmentPriceComponent
                {
                    CataloguePriceId = price.Id, Type = ShipmentPriceComponentType.CatalogueBox,
                    CodeSnapshot = price.Code, DescriptionSnapshot = price.Name, UnitSnapshot = price.Unit,
                    Quantity = selection.Quantity, UnitPrice = price.LowerPrice.Value,
                    LineAmount = amount, SortOrder = index++
                });
            }
        }
        else
        {
            if (!request.Pricing.AgreedAmount.HasValue || request.Pricing.AgreedAmount <= 0 ||
                string.IsNullOrWhiteSpace(request.Pricing.NegotiatedReason))
                throw Rule("NEGOTIATED_DETAILS_REQUIRED",
                    "Negotiated pricing requires a positive agreed amount and an audit reason.", "pricing.negotiatedReason");
            var amount = Money(request.Pricing.AgreedAmount.Value);
            lines.Add(new InvoiceLine { Description = request.Description.Trim(), Quantity = 1,
                Unit = "service", UnitPrice = amount, TaxRate = 0, LineTotal = amount });
            components.Add(new CustomerShipmentPriceComponent
            {
                Type = ShipmentPriceComponentType.Negotiated, CodeSnapshot = "NEGOTIATED",
                DescriptionSnapshot = request.Description.Trim(), UnitSnapshot = "service",
                Quantity = 1, UnitPrice = amount, LineAmount = amount, SortOrder = 0
            });
        }
        var subtotal = lines.Sum(x => Money(x.Quantity * x.UnitPrice));
        var total = lines.Sum(x => x.LineTotal);
        var tax = total - subtotal;
        if (total <= 0) throw Rule("INVALID_INVOICE_TOTAL", "The shipment total must be greater than zero.");
        return new PriceCalculation(components, lines, subtotal, tax, total,
            branch.Currency!, null, request.Pricing.ChargeableWeightKg);
    }

    private async Task<decimal> ApplyCreditsAsync(Invoice invoice,
        IReadOnlyList<CustomerCreditApplicationInput> requested, Guid branchId, Guid idempotencyKey,
        Guid shipmentId, DateTime appliedAt, CancellationToken cancellationToken)
    {
        if (requested.Count == 0) return 0;
        if (requested.Any(x => x.Amount <= 0) ||
            requested.Select(x => x.CreditNoteId).Distinct().Count() != requested.Count)
            throw Rule("INVALID_CREDIT_SELECTION", "Select unique credit notes with positive amounts.",
                "creditApplications");
        var total = Money(requested.Sum(x => x.Amount));
        if (total > invoice.TotalAmount)
            throw Rule("CREDIT_EXCEEDS_INVOICE", "Selected credit exceeds the shipment invoice total.",
                "creditApplications");
        var ids = requested.Select(x => x.CreditNoteId).OrderBy(x => x).ToArray();
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM credit_notes WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE",
            cancellationToken);
        var credits = await _context.CreditNotes.Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var selected in requested)
        {
            if (!credits.TryGetValue(selected.CreditNoteId, out var credit) ||
                credit.BranchId != branchId || credit.CustomerId != invoice.CustomerId ||
                credit.Currency != invoice.Currency ||
                credit.State is CreditNoteState.Voided or CreditNoteState.CreditUsed)
                throw Rule("INCOMPATIBLE_CREDIT",
                    "A selected credit is unavailable for this client, branch or currency.",
                    "creditApplications");
            var amount = Money(selected.Amount);
            if (amount > credit.AvailableAmount)
                throw Rule("CREDIT_AMOUNT_UNAVAILABLE",
                    "A selected amount exceeds the credit note's available balance.", "creditApplications");
            credit.UsedAmount += amount;
            credit.State = credit.AvailableAmount == 0
                ? CreditNoteState.CreditUsed : CreditNoteState.CreditPartUsed;
            credit.RowVersion++;
            _context.CreditApplications.Add(new CreditApplication
            {
                BranchId = branchId, CreditNoteId = credit.Id, InvoiceId = invoice.Id,
                Amount = amount, AppliedAt = appliedAt, AppliedByUserId = CurrentUserId,
                IdempotencyKey = idempotencyKey
            });
        }
        await _accounting.PrepareJournalAsync("ShipmentCreditApplication", shipmentId,
            string.Concat("Apply customer credit to ", invoice.InvoiceNumber), appliedAt, invoice.Currency,
            [new JournalPostingLine("2100", total, 0, invoice.InvoiceNumber),
             new JournalPostingLine("1100", 0, total, invoice.InvoiceNumber)], cancellationToken);
        return total;
    }

    private async Task AddPaymentAsync(Invoice invoice, CustomerShipmentPaymentInput input,
        ActiveBranchScope branch, CustomerShipmentMethod method, DateTime receivedAt,
        CancellationToken cancellationToken)
    {
        var paymentMethod = input.Method!.Value;
        var reference = paymentMethod == PaymentMethod.Cash
            ? string.Concat("CASH-", invoice.InvoiceNumber)
            : input.Reference!.Trim();
        var account = PaymentRouting.AccountCode(paymentMethod);
        var payment = new Payment
        {
            BranchId = branch.BranchId!.Value, InvoiceId = invoice.Id, CustomerId = invoice.CustomerId,
            Reference = reference, Amount = input.AmountPaid, Currency = invoice.Currency,
            Method = paymentMethod, SettlementAccountCode = account, PaidAt = receivedAt,
            Status = PaymentStatus.Confirmed
        };
        var financial = new FinancialTransaction
        {
            BranchId = branch.BranchId.Value,
            TransactionNumber = await _documentNumbers.NextTransactionAsync(cancellationToken),
            Direction = FinancialTransactionDirection.Credit,
            Type = FinancialTransactionType.SalesPayment,
            SourceType = nameof(Payment), SourceId = payment.Id,
            CounterpartyId = invoice.CustomerId, CounterpartyName = invoice.CustomerName,
            Amount = payment.Amount, Currency = payment.Currency, Method = payment.Method,
            SettlementAccountCode = account, ExternalReference = reference, OccurredAt = receivedAt,
            BranchCode = branch.Code!, RecordedByUserId = CurrentUserId,
            FreightClassification = method == CustomerShipmentMethod.Air
                ? FreightClassification.Air : FreightClassification.Sea,
            FreightClassifiedAt = receivedAt
        };
        var receipt = new TransactionDocument
        {
            DocumentNumber = await _documentNumbers.NextFinancialDocumentAsync(
                FinancialDocumentType.Receipt, cancellationToken),
            Type = FinancialDocumentType.Receipt, FinancialTransactionId = financial.Id,
            InvoiceId = invoice.Id, PaymentId = payment.Id, IssuedAt = receivedAt,
            IssuedByUserId = CurrentUserId, VerificationCode = DocumentSecurity.NewVerificationCode()
        };
        var journal = await _accounting.PrepareJournalAsync("Payment", payment.Id,
            string.Concat("Receive payment ", reference), receivedAt, payment.Currency,
            [
                new JournalPostingLine(account, payment.Amount, 0, string.Concat("Payment ", reference)),
                new JournalPostingLine("1100", 0, payment.Amount,
                    string.Concat("Settlement of ", invoice.InvoiceNumber))
            ], cancellationToken);
        financial.JournalEntryId = journal.Id;
        _context.Payments.Add(payment);
        _context.FinancialTransactions.Add(financial);
        _context.TransactionDocuments.Add(receipt);
    }

    private async Task AwardReferralAsync(Customer referrer, Customer referred, Invoice invoice,
        CancellationToken cancellationToken)
    {
        var account = await _context.CustomerLoyaltyAccounts.FirstOrDefaultAsync(x =>
            x.CustomerId == referrer.Id, cancellationToken);
        if (account is null)
        {
            account = new CustomerLoyaltyAccount { CustomerId = referrer.Id };
            _context.CustomerLoyaltyAccounts.Add(account);
        }
        const int points = 10;
        account.AvailablePoints += points;
        account.LifetimeEarnedPoints += points;
        _context.LoyaltyPointTransactions.Add(new LoyaltyPointTransaction
        {
            CustomerLoyaltyAccountId = account.Id, CustomerId = referrer.Id,
            ReferredCustomerId = referred.Id, InvoiceId = invoice.Id,
            Type = LoyaltyPointTransactionType.ReferralShipmentAward, Points = points,
            Description = string.Concat("Referral shipment ", invoice.InvoiceNumber),
            OccurredAt = DateTime.UtcNow
        });
    }

    private async Task<IReadOnlyList<CustomerShipmentResponse>> LoadResponsesAsync(
        IReadOnlyCollection<Guid> shipmentIds, Guid? branchId, CancellationToken cancellationToken)
    {
        var rows = await (from shipment in _context.CustomerShipments.AsNoTracking()
                          join invoice in _context.Invoices.AsNoTracking() on shipment.InvoiceId equals invoice.Id
                          join client in _context.Customers.AsNoTracking() on shipment.CustomerId equals client.Id
                          join branch in _context.Branches.AsNoTracking() on shipment.BranchId equals branch.Id
                          where shipmentIds.Contains(shipment.Id) && (!branchId.HasValue || shipment.BranchId == branchId)
                          select new { Shipment = shipment, Invoice = invoice, Client = client, Branch = branch })
            .ToListAsync(cancellationToken);
        var invoiceIds = rows.Select(x => x.Invoice.Id).ToList();
        var invoiceLines = await _context.InvoiceLines.AsNoTracking().Where(x => invoiceIds.Contains(x.InvoiceId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var components = await _context.CustomerShipmentPriceComponents.AsNoTracking()
            .Where(x => shipmentIds.Contains(x.CustomerShipmentId)).OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id).ToListAsync(cancellationToken);
        return shipmentIds.Select(id => rows.FirstOrDefault(x => x.Shipment.Id == id)).Where(x => x is not null)
            .Select(row =>
            {
                var shipment = row!.Shipment;
                var invoice = row.Invoice;
                return new CustomerShipmentResponse(shipment.Id, shipment.InvoiceNumber,
                    shipment.RowVersion, shipment.BranchId, BranchKey(row.Branch), invoice.Currency,
                    row.Client.Id, row.Client.ClientNumber, row.Client.FullName, row.Client.Phone,
                    row.Client.Email, shipment.ReceiverName, shipment.Method, shipment.OriginCountryCode,
                    shipment.OriginCity, shipment.DestinationCountryCode, shipment.DestinationCityOrState,
                    shipment.DeliveryAddress, shipment.HandoverMethod, shipment.GoodsType,
                    shipment.Description, shipment.Contents, shipment.PieceCount, shipment.PricingMethod,
                    shipment.ServiceCodeSnapshot, shipment.ActualWeightKg, shipment.ChargeableWeightKg,
                    shipment.LengthCm, shipment.WidthCm, shipment.HeightCm, shipment.NegotiatedReason,
                    shipment.MovementState, PaymentState(invoice.PaymentStatus), invoice.Subtotal,
                    invoice.TaxAmount, invoice.TotalAmount, invoice.AmountPaid, invoice.AppliedCreditAmount,
                    invoice.BalanceDue, invoice.PreferredPaymentMethod, shipment.ReferredByClientId,
                    shipment.CustomerNote, shipment.CreatedAt, shipment.LastModifiedAt,
                    components.Where(x => x.CustomerShipmentId == shipment.Id).Select(x =>
                        new CustomerShipmentPriceComponentResponse(x.Id, x.CataloguePriceId, x.Type,
                            x.CodeSnapshot, x.DescriptionSnapshot, x.UnitSnapshot, x.Quantity,
                            x.UnitPrice, x.LineAmount)).ToList(),
                    invoiceLines.Where(x => x.InvoiceId == invoice.Id).Select(x =>
                        new CustomerShipmentInvoiceLineResponse(x.Id, x.Description, x.Quantity,
                            x.Unit, x.UnitPrice, x.TaxRate, x.LineTotal)).ToList());
            }).ToList();
    }

    private Guid RequireIdempotencyKey()
    {
        var value = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!Guid.TryParse(value, out var key))
            throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED",
                "Idempotency key required", "Send a UUID in the Idempotency-Key header.");
        return key;
    }

    private long RequireVersion()
    {
        var value = Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        if (!long.TryParse(value, out var version) || version < 1)
            throw new ApiProblemException(428, "IF_MATCH_REQUIRED", "Version required",
                "Send the current row version in the If-Match header.");
        return version;
    }

    private static void ValidateRequest(CreateCustomerShipmentRequest request, ActiveBranchScope branch)
    {
        if (request.Client is null || string.IsNullOrWhiteSpace(request.Client.Name) ||
            string.IsNullOrWhiteSpace(request.Client.Phone))
            throw Rule("CLIENT_DETAILS_REQUIRED", "Client name and phone are required.", "client");
        if (!string.IsNullOrWhiteSpace(request.Client.Email) &&
            !new EmailAddressAttribute().IsValid(request.Client.Email.Trim()))
            throw Rule("INVALID_CLIENT_EMAIL", "Client email is optional, but must be valid when supplied.", "client.email");
        if (string.IsNullOrWhiteSpace(request.ReceiverName))
            throw Rule("RECEIVER_NAME_REQUIRED", "Receiver name is required.", "receiverName");
        if (request.Pricing is null || request.Payment is null || request.InvoiceLines is null ||
            request.CreditApplications is null || request.Pricing.BoxItems is null)
            throw Rule("PRICING_AND_PAYMENT_REQUIRED", "Pricing, invoice lines and payment details are required.");
        if (!Enum.IsDefined(request.Method) || !Enum.IsDefined(request.HandoverMethod) ||
            !Enum.IsDefined(request.GoodsType) || !Enum.IsDefined(request.Pricing.Method))
            throw Rule("INVALID_SHIPMENT_OPTION", "Select valid shipment, handover, goods and pricing values.");
        if (request.Destination is null || string.IsNullOrWhiteSpace(request.Destination.CountryCode) ||
            string.IsNullOrWhiteSpace(request.Destination.CityOrState))
            throw Rule("DESTINATION_REQUIRED", "Destination country and city/state are required.", "destination");
        var destinationCountry = request.Destination.CountryCode.Trim().ToUpperInvariant();
        if (destinationCountry == branch.CountryCode && string.Equals(request.Destination.CityOrState.Trim(),
                branch.City, StringComparison.OrdinalIgnoreCase))
            throw Rule("DESTINATION_EQUALS_ORIGIN", "Destination cannot be the selected origin hub.", "destination");
        if (request.HandoverMethod == ShipmentHandoverMethod.Delivery &&
            string.IsNullOrWhiteSpace(request.Destination.Address))
            throw Rule("DELIVERY_ADDRESS_REQUIRED", "Delivery requires an address.", "destination.address");
        if (string.IsNullOrWhiteSpace(request.Description) || request.PieceCount <= 0)
            throw Rule("SHIPMENT_ITEMS_REQUIRED", "Description and a positive whole piece count are required.");
    }

    private static void ValidateUpdate(UpdateCustomerShipmentRequest request, ActiveBranchScope branch)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw Rule("EDIT_REASON_REQUIRED", "An edit reason is required.", "reason");
        if (string.IsNullOrWhiteSpace(request.ReceiverName) || string.IsNullOrWhiteSpace(request.Description) ||
            request.PieceCount <= 0 || request.Destination is null ||
            string.IsNullOrWhiteSpace(request.Destination.CountryCode) ||
            string.IsNullOrWhiteSpace(request.Destination.CityOrState))
            throw Rule("INVALID_SHIPMENT_UPDATE", "Receiver, destination, description and pieces are required.");
        if (request.HandoverMethod == ShipmentHandoverMethod.Delivery &&
            string.IsNullOrWhiteSpace(request.Destination.Address))
            throw Rule("DELIVERY_ADDRESS_REQUIRED", "Delivery requires an address.", "destination.address");
        if (request.Destination.CountryCode.Trim().Equals(branch.CountryCode, StringComparison.OrdinalIgnoreCase) &&
            request.Destination.CityOrState.Trim().Equals(branch.City, StringComparison.OrdinalIgnoreCase))
            throw Rule("DESTINATION_EQUALS_ORIGIN", "Destination cannot be the selected origin hub.", "destination");
    }

    private static void ValidateInvoiceLine(CustomerShipmentInvoiceLineInput line)
    {
        if (string.IsNullOrWhiteSpace(line.Description) || string.IsNullOrWhiteSpace(line.Unit) ||
            line.Quantity <= 0 || line.UnitPrice < 0 || line.TaxRate is < 0 or > 100)
            throw Rule("INVALID_INVOICE_LINE",
                "Each invoice line requires description, unit, positive quantity, non-negative rate and valid tax.",
                "invoiceLines");
    }

    private static void ValidatePayment(CustomerShipmentPaymentInput payment, decimal amountToPay)
    {
        amountToPay = Money(amountToPay);
        if (payment.AmountPaid < 0 || payment.AmountPaid > amountToPay)
            throw Rule("INVALID_PAYMENT_AMOUNT", "Amount paid cannot be negative or exceed the shipment total.",
                "payment.amountPaid");
        var validState = payment.State switch
        {
            ShipmentPaymentState.Paid => payment.AmountPaid == amountToPay,
            ShipmentPaymentState.Partial => payment.AmountPaid > 0 && payment.AmountPaid < amountToPay,
            ShipmentPaymentState.Unpaid => payment.AmountPaid == 0 && amountToPay > 0,
            _ => false
        };
        if (!validState)
            throw Rule("PAYMENT_STATE_MISMATCH", "Paid, Partial or Unpaid must reconcile to the exact amounts.",
                "payment.state");
        if (payment.AmountPaid == 0) return;
        if (!payment.Method.HasValue || !PaymentRouting.IsSupported(payment.Method.Value))
            throw Rule("PAYMENT_METHOD_REQUIRED", "Money received requires Cash, Card or BankTransfer.",
                "payment.method");
        if (payment.Method is PaymentMethod.Card or PaymentMethod.BankTransfer &&
            string.IsNullOrWhiteSpace(payment.Reference))
            throw Rule("PAYMENT_REFERENCE_REQUIRED", "Card and bank transfer payments require a reference.",
                "payment.reference");
    }

    private static void ApplyPaymentState(Invoice invoice, decimal paid)
    {
        invoice.AmountPaid = paid;
        if (invoice.BalanceDue == 0)
        { invoice.PaymentStatus = InvoicePaymentStatus.Paid; invoice.Status = InvoiceStatus.Paid; }
        else if (invoice.AmountPaid > 0 || invoice.AppliedCreditAmount > 0)
        { invoice.PaymentStatus = InvoicePaymentStatus.PartiallyPaid; invoice.Status = InvoiceStatus.PartiallyPaid; }
        else
        { invoice.PaymentStatus = InvoicePaymentStatus.Unpaid; invoice.Status = InvoiceStatus.Issued; }
    }

    private static ApiProblemException DuplicateClient(ClientDuplicateReviewResult result) =>
        new(409, "CLIENT_MATCH_FOUND", "These client details match an existing record",
            "Choose the existing client or resolve the possible match.",
            extensions: new Dictionary<string, object?>
            {
                ["duplicateReviewId"] = result.ReviewId,
                ["hasExactConflict"] = result.HasExactConflict,
                ["matches"] = result.Matches
            });

    private static ApiProblemException Rule(string code, string detail, string? field = null) =>
        new(422, code, "Shipment rule was not satisfied", detail,
            field is null ? null : new Dictionary<string, string[]> { [field] = [detail] });
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string InvoiceDigits(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string QuoteVersion(long value) => string.Concat("\"", value, "\"");
    private static string BranchKey(Branch branch) => branch.Code switch
    { "UK-MAN" => "manchester", "NG-LOS" => "lagos", "CN-GZ" => "guangzhou", _ => branch.Code };
    private static ShipmentPaymentState PaymentState(InvoicePaymentStatus value) => value switch
    { InvoicePaymentStatus.Paid => ShipmentPaymentState.Paid,
      InvoicePaymentStatus.PartiallyPaid => ShipmentPaymentState.Partial,
      _ => ShipmentPaymentState.Unpaid };
    private static string PersonName(string value)
    {
        var collapsed = string.Join(' ', value.Trim().Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
        return CultureInfo.GetCultureInfo("en-GB").TextInfo.ToTitleCase(collapsed.ToLowerInvariant());
    }

    private sealed record CreateResult(Guid ShipmentId, bool Created);
    private sealed record PriceCalculation(IReadOnlyList<CustomerShipmentPriceComponent> Components,
        IReadOnlyList<InvoiceLine> InvoiceLines, decimal Subtotal, decimal Tax, decimal Total,
        string Currency, string? ServiceCode, decimal? ChargeableWeightKg);
}

public sealed record CancelCustomerShipmentRequest(string Reason);
