using Api.Services;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>Minimal branch-scoped client directory used by shipments, sales and reports.</summary>
[Authorize(Policy = "StaffOnly")]
[Route("api/clients")]
public sealed class ClientsController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IClientIdentityService _identity;
    private readonly IActiveBranchContext _branches;
    private readonly ICompanyDocumentNumberGenerator _numbers;

    public ClientsController(ApplicationDbContext context, IClientIdentityService identity,
        IActiveBranchContext branches, ICompanyDocumentNumberGenerator numbers)
    {
        _context = context;
        _identity = identity;
        _branches = branches;
        _numbers = numbers;
    }

    [HttpPost("match-check")]
    public async Task<ActionResult<ClientDuplicateReviewResult>> MatchCheck(CheckClientMatchRequest request,
        CancellationToken cancellationToken) => Ok(await _identity.CheckAsync(new ClientIdentityInput(
            request.FullName, request.Email, request.Phone, request.InvoiceNumber), null, cancellationToken));

    [HttpPost("duplicate-reviews/{reviewId:guid}/resolve")]
    public async Task<ActionResult<ClientDuplicateReviewResult>> ResolveDuplicate(Guid reviewId,
        ResolveClientMatchRequest request, CancellationToken cancellationToken) => Ok(await _identity.ResolveAsync(
            reviewId, request.Decision, request.SelectedCustomerId, request.Reason, cancellationToken));

    [HttpGet]
    public async Task<ActionResult> Search([FromQuery] string? search,
        [FromQuery] DateTime? addedFrom, [FromQuery] DateTime? addedTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = true, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.Customers.AsNoTracking().Where(x => x.MergedIntoCustomerId == null &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId));
        if (addedFrom.HasValue) query = query.Where(x => x.CreatedAt >= addedFrom.Value.ToUniversalTime());
        if (addedTo.HasValue) query = query.Where(x =>
            x.CreatedAt < addedTo.Value.ToUniversalTime().Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.FullName.ToLower().Contains(term) ||
                (x.Email != null && x.Email.ToLower().Contains(term)) ||
                x.Phone.Contains(term) || x.ClientNumber.ToLower().Contains(term));
        }
        var projected = query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.FullName)
            .ThenBy(x => x.Id).Select(x => new
            {
                Id = x.Id, Name = x.FullName, x.Phone, x.Email, AddedAt = x.CreatedAt,
                HubId = x.BranchId, x.ClientNumber,
                PatronageCount = _context.CustomerShipments.Count(shipment =>
                    shipment.CustomerId == x.Id),
                ReferralCount = _context.CustomerShipments.Count(shipment =>
                    shipment.ReferredByClientId == x.Id),
                LoyaltyPoints = _context.CustomerLoyaltyAccounts.Where(account =>
                        account.CustomerId == x.Id)
                    .Select(account => account.AvailablePoints)
                    .FirstOrDefault(),
                LastShipmentAt = _context.CustomerShipments.Where(shipment =>
                        shipment.CustomerId == x.Id)
                    .OrderByDescending(shipment => shipment.CreatedAt)
                    .Select(shipment => (DateTime?)shipment.CreatedAt)
                    .FirstOrDefault()
            });
        return Ok(await projected.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult> Create(CreateClientDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        ValidateContact(request.Name, request.Phone, request.Email);
        var input = new ClientIdentityInput(request.Name, request.Email, request.Phone, null);
        var selected = await _identity.GetSelectedExistingClientAsync(input, request.DuplicateReviewId,
            cancellationToken);
        if (selected.HasValue)
            return Conflict(new { Code = "EXISTING_CLIENT_SELECTED", ClientId = selected.Value,
                Message = "Use the selected existing client instead of creating another record." });
        var duplicate = await _identity.ValidateNewClientAsync(input, request.DuplicateReviewId, null,
            cancellationToken);
        if (duplicate is not null) return Conflict(duplicate);
        var customer = new Customer(Guid.NewGuid(), request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            0, branch.CountryCode!)
        {
            ClientNumber = await _numbers.NextClientAsync(cancellationToken),
            BranchId = branch.BranchId!.Value,
            Phone = request.Phone.Trim(),
            WalletCurrency = branch.Currency!
        };
        _context.Customers.Add(customer);
        _context.NotificationPreferences.Add(new NotificationPreference { CustomerId = customer.Id });
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { clientId = customer.Id }, Map(customer, branch.Name!));
    }

    [HttpGet("{clientId:guid}")]
    public async Task<ActionResult> Get(Guid clientId, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var customer = await (from client in _context.Customers.AsNoTracking()
            join hub in _context.Branches.AsNoTracking() on client.BranchId equals hub.Id
            where client.Id == clientId && client.MergedIntoCustomerId == null &&
                  (branch.IsAllBranches || client.BranchId == branch.BranchId)
            select new { Client = client, Hub = hub }).FirstOrDefaultAsync(cancellationToken);
        return customer is null ? NotFound() : Ok(Map(customer.Client, customer.Hub.Name));
    }

    [HttpPatch("{clientId:guid}")]
    public async Task<ActionResult> Update(Guid clientId, UpdateClientDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        ValidateContact(request.Name, request.Phone, request.Email);
        var customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == clientId &&
            x.BranchId == branch.BranchId && x.MergedIntoCustomerId == null, cancellationToken);
        if (customer is null) return NotFound();
        var duplicate = await _identity.ValidateNewClientAsync(new ClientIdentityInput(request.Name,
            request.Email, request.Phone, null), request.DuplicateReviewId, customer.Id, cancellationToken);
        if (duplicate is not null) return Conflict(duplicate);
        customer.FullName = request.Name.Trim();
        customer.Phone = request.Phone.Trim();
        customer.Email = string.IsNullOrWhiteSpace(request.Email)
            ? null : request.Email.Trim().ToLowerInvariant();
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(Map(customer, branch.Name!));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{clientId:guid}/merge")]
    public async Task<ActionResult> Merge(Guid clientId, MergeClientRequest request,
        CancellationToken cancellationToken)
    {
        if (clientId == request.TargetClientId) return BadRequest("Select a different target client.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("A merge reason is required.");
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var source = await _context.Customers.FirstOrDefaultAsync(x => x.Id == clientId &&
            x.BranchId == branch.BranchId && x.MergedIntoCustomerId == null, cancellationToken);
        var target = await _context.Customers.FirstOrDefaultAsync(x => x.Id == request.TargetClientId &&
            x.BranchId == branch.BranchId && x.MergedIntoCustomerId == null, cancellationToken);
        if (source is null || target is null) return NotFound("Source or target client was not found.");
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Invoices.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id)
                .SetProperty(y => y.CustomerName, target.FullName), cancellationToken);
        await _context.CustomerShipments.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id), cancellationToken);
        await _context.CustomerShipments.Where(x => x.ReferredByClientId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ReferredByClientId, target.Id), cancellationToken);
        await _context.Payments.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id), cancellationToken);
        await _context.CargoItems.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id), cancellationToken);
        await _context.CreditNotes.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id), cancellationToken);
        await _context.CommunicationMessages.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id), cancellationToken);
        await _context.OperationalManifestEntries.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id)
                .SetProperty(y => y.CustomerNameSnapshot, target.FullName), cancellationToken);
        await _context.ShippingQuotes.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id)
                .SetProperty(y => y.ContactName, target.FullName), cancellationToken);
        await _context.Appointments.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id), cancellationToken);
        await _context.Contacts.Where(x => x.CustomerId == source.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.CustomerId, target.Id), cancellationToken);
        source.MergedIntoCustomerId = target.Id;
        source.MergedAt = DateTime.UtcNow;
        source.CompanyName = $"Merged into {target.ClientNumber}: {request.Reason.Trim()}";
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { SourceClientId = source.Id, TargetClientId = target.Id,
            target.ClientNumber, target.FullName, source.MergedAt });
    }

    [HttpGet("{clientId:guid}/history")]
    public async Task<ActionResult> History(Guid clientId, [FromQuery] string? cursor,
        [FromQuery] string? type, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var client = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId), cancellationToken);
        if (client is null) return NotFound();
        var invoiceIds = await _context.Invoices.AsNoTracking().Where(x => x.CustomerId == clientId)
            .Select(x => x.Id.ToString()).ToListAsync(cancellationToken);
        var shipmentIds = await _context.CustomerShipments.AsNoTracking()
            .Where(x => x.CustomerId == clientId).Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);
        var entityIds = invoiceIds.Concat(shipmentIds).Append(clientId.ToString()).ToArray();
        var query = _context.AuditLogs.AsNoTracking().Where(x => entityIds.Contains(x.EntityId));
        if (!string.IsNullOrWhiteSpace(type))
        {
            var value = type.Trim().ToLowerInvariant();
            query = query.Where(x => x.EntityType.ToLower().Contains(value) ||
                x.Action.ToLower().Contains(value));
        }
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var position = PaginationExtensions.DecodeCursor(cursor);
            query = query.Where(x => x.CreatedAt < position.Timestamp ||
                (x.CreatedAt == position.Timestamp && x.Id.CompareTo(position.Id) < 0));
        }
        pageSize = new[] { 10, 25, 50, 100 }.Contains(pageSize) ? pageSize : 25;
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var next = hasMore && rows.Count > 0
            ? PaginationExtensions.EncodeCursor(rows[^1].CreatedAt, rows[^1].Id) : null;
        return Ok(new CursorPagedResponse<AuditLog>(rows, pageSize, hasMore, next));
    }

    [HttpGet("{clientId:guid}/summary")]
    public async Task<ActionResult> Summary(Guid clientId, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var client = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId &&
            x.MergedIntoCustomerId == null && (branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken);
        if (client is null) return NotFound();
        var invoices = await _context.Invoices.AsNoTracking().Where(x => x.CustomerId == clientId &&
            x.Status != Domain.Enums.InvoiceStatus.Void).ToListAsync(cancellationToken);
        var shipments = await _context.CustomerShipments.AsNoTracking()
            .Where(x => x.CustomerId == clientId).ToListAsync(cancellationToken);
        var referrals = await _context.CustomerShipments.AsNoTracking()
            .CountAsync(x => x.ReferredByClientId == clientId, cancellationToken);
        return Ok(new
        {
            Client = Map(client, branch.Name ?? branch.BranchKey ?? "Hub"),
            PatronageCount = shipments.Count,
            ReferralCount = referrals,
            ByCurrency = invoices.GroupBy(x => x.Currency).Select(x => new
            {
                Currency = x.Key, TotalSpend = x.Sum(y => y.TotalAmount),
                Paid = x.Sum(y => y.AmountPaid), Balance = x.Sum(y => y.BalanceDue)
            }),
            LastShipmentAt = shipments.OrderByDescending(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt).FirstOrDefault()
        });
    }

    [HttpGet("summary/contact-completeness")]
    public async Task<ActionResult> ContactCompleteness(CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var rows = await _context.Customers.AsNoTracking().Where(x => x.MergedIntoCustomerId == null &&
                (branch.IsAllBranches || x.BranchId == branch.BranchId))
            .Select(x => new { HasPhone = x.Phone != "", HasEmail = x.Email != null && x.Email != "" })
            .ToListAsync(cancellationToken);
        var total = rows.Count;
        decimal Percent(int count) => total == 0 ? 0 : Math.Round(count * 100m / total, 2);
        return Ok(new
        {
            TotalClients = total,
            WithPhonePercent = Percent(rows.Count(x => x.HasPhone)),
            WithEmailPercent = Percent(rows.Count(x => x.HasEmail)),
            WithBothPercent = Percent(rows.Count(x => x.HasPhone && x.HasEmail)),
            MissingBoth = rows.Count(x => !x.HasPhone && !x.HasEmail)
        });
    }

    private static object Map(Customer value, string hub) => new
    {
        Id = value.Id, Name = value.FullName, value.Phone, value.Email,
        AddedAt = value.CreatedAt, HubId = value.BranchId, Hub = hub, value.ClientNumber
    };

    private static void ValidateContact(string name, string phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Client name is required.");
        if (string.IsNullOrWhiteSpace(phone)) throw new ArgumentException("Client phone number is required.");
        if (!string.IsNullOrWhiteSpace(email) &&
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("Client email address is invalid.");
    }
}
