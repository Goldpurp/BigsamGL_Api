using Api.Exceptions;
using Api.Services;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize(Policy = "StaffOnly")]
[EnableRateLimiting("Search")]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;

    public SearchController(ApplicationDbContext context, IActiveBranchContext branches)
    {
        _context = context;
        _branches = branches;
    }

    [HttpGet]
    public async Task<ActionResult> Search([FromQuery] string q, [FromQuery] string? types,
        [FromQuery] int limit = 25, CancellationToken cancellationToken = default)
    {
        var query = Normalize(q);
        if (query.Length < 2)
            throw new ApiProblemException(422, "SEARCH_QUERY_TOO_SHORT", "Enter a longer search",
                "Use at least two characters; invoice-fragment lookup requires four digits.");
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var wanted = ParseTypes(types);
        var take = Math.Clamp(limit, 1, 50);
        var perType = Math.Min(take, 20);
        var results = new List<SearchResult>();
        bool Wants(params string[] values) => wanted.Count == 0 || values.Any(wanted.Contains);

        if (Wants("shipment", "invoice", "customer-shipment"))
        {
            var rows = await (from shipment in _context.CustomerShipments.AsNoTracking()
                              join client in _context.Customers.AsNoTracking()
                                  on shipment.CustomerId equals client.Id
                              where (branch.IsAllBranches || shipment.BranchId == branch.BranchId) &&
                                  (shipment.InvoiceNumber.ToLower().Contains(query) ||
                                   shipment.InvoiceNumericFragment.Contains(query) ||
                                   client.FullName.ToLower().Contains(query) ||
                                   client.ClientNumber.ToLower().Contains(query))
                              orderby shipment.CreatedAt descending
                              select new { Shipment = shipment, Client = client })
                .Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult("customer-shipment", x.Shipment.Id,
                x.Shipment.InvoiceNumber, x.Client.FullName,
                $"{x.Shipment.Method} · {x.Shipment.MovementState} · {x.Shipment.OriginCity} → {x.Shipment.DestinationCityOrState}",
                $"/customer-shipments/{x.Shipment.Id}", Rank(query, x.Shipment.InvoiceNumber,
                    x.Shipment.InvoiceNumericFragment, x.Client.FullName))));
        }

        if (Wants("client"))
        {
            var rows = await _context.Customers.AsNoTracking().Where(x =>
                    (branch.IsAllBranches || x.BranchId == branch.BranchId) &&
                    (x.FullName.ToLower().Contains(query) || x.ClientNumber.ToLower().Contains(query) ||
                     x.Phone.Contains(query) || (x.Email != null && x.Email.ToLower().Contains(query))))
                .OrderByDescending(x => x.CreatedAt).Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult("client", x.Id, x.ClientNumber,
                x.FullName, string.Join(" · ", new[] { x.Phone, x.Email }
                    .Where(v => !string.IsNullOrWhiteSpace(v))),
                $"/contacts/clients/{x.Id}", Rank(query, x.ClientNumber, x.FullName, x.Phone, x.Email))));
        }

        if (Wants("manifest", "tracking"))
        {
            var rows = await (from manifest in _context.OperationalManifests.AsNoTracking()
                              join connection in _context.ManifestTrackingConnections.AsNoTracking()
                                  on manifest.Id equals connection.ManifestId into connections
                              from connection in connections.DefaultIfEmpty()
                              where (branch.IsAllBranches || manifest.BranchId == branch.BranchId) &&
                                  (manifest.ManifestNumber.ToLower().Contains(query) ||
                                   manifest.ShipmentId.ToLower().Contains(query) ||
                                   manifest.ShipmentBookingId.ToLower().Contains(query) ||
                                   (manifest.ContainerOrFlightNumber != null &&
                                    manifest.ContainerOrFlightNumber.ToLower().Contains(query)) ||
                                   (manifest.BillOfLadingNumber != null &&
                                    manifest.BillOfLadingNumber.ToLower().Contains(query)) ||
                                   (manifest.CarrierReference != null &&
                                    manifest.CarrierReference.ToLower().Contains(query)) ||
                                   (connection != null && (connection.ExternalReference.ToLower().Contains(query) ||
                                    connection.ShippingCompanyName.ToLower().Contains(query))))
                              orderby manifest.ManifestDate descending
                              select new { Manifest = manifest, Connection = connection })
                .Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult(x.Connection is null ? "manifest" : "tracking",
                x.Manifest.Id, x.Manifest.ManifestNumber, x.Manifest.ShipmentId,
                $"{x.Manifest.LogisticsMethod} · {x.Manifest.OriginCity} → {x.Manifest.DestinationCity}" +
                (x.Connection is null ? string.Empty :
                    $" · {x.Connection.ShippingCompanyName} · {x.Connection.ExternalReference}"),
                $"/operations/manifests/{x.Manifest.Id}", Rank(query, x.Manifest.ManifestNumber,
                    x.Manifest.ShipmentId, x.Manifest.ShipmentBookingId,
                    x.Manifest.ContainerOrFlightNumber, x.Manifest.BillOfLadingNumber,
                    x.Manifest.CarrierReference, x.Connection?.ExternalReference))));
        }

        if (Wants("pickup", "schedule"))
        {
            var rows = await _context.ScheduledPickups.AsNoTracking().Where(x => x.DeletedAt == null &&
                    (branch.IsAllBranches || x.BranchId == branch.BranchId) &&
                    (x.PickupNumber.ToLower().Contains(query) || x.ContactName.ToLower().Contains(query) ||
                     x.Phone.Contains(query) || x.Address.ToLower().Contains(query)))
                .OrderByDescending(x => x.ScheduledAt).Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult("pickup", x.Id, x.PickupNumber,
                x.ContactName, $"{x.State} · {x.City} · {x.ScheduledAt:u}",
                $"/schedule/pickups/{x.Id}", Rank(query, x.PickupNumber, x.ContactName, x.Phone))));
        }

        if (Wants("receipt", "transaction"))
        {
            var rows = await (from transaction in _context.FinancialTransactions.AsNoTracking()
                              join document in _context.TransactionDocuments.AsNoTracking()
                                  on transaction.Id equals document.FinancialTransactionId into documents
                              from document in documents.DefaultIfEmpty()
                              where (branch.IsAllBranches || transaction.BranchId == branch.BranchId) &&
                                  (transaction.TransactionNumber.ToLower().Contains(query) ||
                                   transaction.ExternalReference.ToLower().Contains(query) ||
                                   transaction.CounterpartyName.ToLower().Contains(query) ||
                                   (document != null && document.DocumentNumber.ToLower().Contains(query)))
                              orderby transaction.OccurredAt descending
                              select new { Transaction = transaction, Document = document })
                .Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult(
                x.Document?.Type == FinancialDocumentType.Receipt ? "receipt" : "transaction",
                x.Transaction.Id, x.Document?.DocumentNumber ?? x.Transaction.TransactionNumber,
                x.Transaction.CounterpartyName,
                $"{x.Transaction.Direction} · {x.Transaction.Currency} {x.Transaction.Amount:N2} · {x.Transaction.Status}",
                $"/finance/transactions/{x.Transaction.Id}", Rank(query, x.Transaction.TransactionNumber,
                    x.Transaction.ExternalReference, x.Document?.DocumentNumber))));
        }

        if (Wants("credit-note", "credit"))
        {
            var rows = await (from credit in _context.CreditNotes.AsNoTracking()
                              join client in _context.Customers.AsNoTracking()
                                  on credit.CustomerId equals client.Id
                              where (branch.IsAllBranches || credit.BranchId == branch.BranchId) &&
                                  (credit.CreditNoteNumber.ToLower().Contains(query) ||
                                   client.FullName.ToLower().Contains(query))
                              orderby credit.IssuedAt descending
                              select new { Credit = credit, Client = client })
                .Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult("credit-note", x.Credit.Id,
                x.Credit.CreditNoteNumber, x.Client.FullName,
                $"{x.Credit.State} · {x.Credit.Currency} {x.Credit.AvailableAmount:N2} available",
                $"/finance/credit-notes/{x.Credit.Id}",
                Rank(query, x.Credit.CreditNoteNumber, x.Client.FullName))));
        }

        if (Wants("issue", "cargo-item-issue"))
        {
            var rows = await _context.CargoItemIssues.AsNoTracking().Where(x =>
                    (branch.IsAllBranches || x.BranchId == branch.BranchId) &&
                    (x.IssueNumber.ToLower().Contains(query) ||
                     (x.InvoiceNumber != null && x.InvoiceNumber.ToLower().Contains(query)) ||
                     x.ExpectedDetails.ToLower().Contains(query) ||
                     x.FoundDetails.ToLower().Contains(query)))
                .OrderByDescending(x => x.CreatedAt).Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult("cargo-item-issue", x.Id, x.IssueNumber,
                x.InvoiceNumber ?? x.Type.ToString(), $"{x.Severity} · {x.State} · {x.FoundDetails}",
                $"/operations/item-issues/{x.Id}", Rank(query, x.IssueNumber, x.InvoiceNumber))));
        }

        if (Wants("purchase", "supplier"))
        {
            var rows = await (from purchase in _context.CompanyPurchases.AsNoTracking()
                              join supplier in _context.Suppliers.AsNoTracking()
                                  on purchase.SupplierId equals (Guid?)supplier.Id into suppliers
                              from supplier in suppliers.DefaultIfEmpty()
                              where (branch.IsAllBranches || purchase.BranchCode == branch.Code) &&
                                  (purchase.PurchaseNumber.ToLower().Contains(query) ||
                                   purchase.VendorName.ToLower().Contains(query) ||
                                   purchase.SupplierReference.ToLower().Contains(query) ||
                                   (supplier != null && (supplier.Name.ToLower().Contains(query) ||
                                    supplier.SupplierNumber.ToLower().Contains(query))))
                              orderby purchase.PurchaseDate descending
                              select new { Purchase = purchase, Supplier = supplier })
                .Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult("purchase", x.Purchase.Id,
                x.Purchase.PurchaseNumber, x.Purchase.VendorName,
                $"{x.Purchase.Type} · {x.Purchase.Status} · {x.Purchase.Currency} {x.Purchase.TotalAmount:N2}",
                $"/purchases/{x.Purchase.Id}", Rank(query, x.Purchase.PurchaseNumber,
                    x.Purchase.VendorName, x.Purchase.SupplierReference, x.Supplier?.SupplierNumber))));
        }

        if (Wants("inventory", "inventory-item"))
        {
            var rows = await (from item in _context.InventoryItems.AsNoTracking()
                              join stock in _context.InventoryStockLevels.AsNoTracking()
                                  on item.Id equals stock.InventoryItemId
                              join location in _context.InventoryLocations.AsNoTracking()
                                  on stock.InventoryLocationId equals location.Id
                              where (branch.IsAllBranches || location.BranchId == branch.BranchId) &&
                                  (item.Sku.ToLower().Contains(query) ||
                                   item.Name.ToLower().Contains(query) ||
                                   (item.Barcode != null && item.Barcode.Contains(query)))
                              orderby item.Name
                              select new { Item = item, Location = location, stock.QuantityOnHand })
                .Take(perType).ToListAsync(cancellationToken);
            results.AddRange(rows.Select(x => new SearchResult("inventory-item", x.Item.Id, x.Item.Sku,
                x.Item.Name, $"{x.Location.Name} · {x.QuantityOnHand:N2} {x.Item.UnitOfMeasure}",
                $"/inventory/items/{x.Item.Id}", Rank(query, x.Item.Sku, x.Item.Barcode, x.Item.Name))));
        }

        var ordered = results.OrderBy(x => x.Rank).ThenBy(x => x.Type).ThenBy(x => x.Reference)
            .GroupBy(x => new { x.Type, x.Id }).Select(x => x.First()).Take(take)
            .Select(x => new { x.Type, x.Id, x.Reference, x.Title, x.Subtitle, x.Route }).ToList();
        return Ok(new { Query = query, ActiveBranch = branch.BranchKey, Count = ordered.Count,
            Limit = take, Results = ordered });
    }

    [HttpGet("shipment-by-invoice-fragment/{digits}")]
    public async Task<ActionResult> ShipmentByInvoiceFragment(string digits,
        CancellationToken cancellationToken)
    {
        digits = Normalize(digits);
        if (digits.Length is < 4 or > 12 || digits.Any(x => !char.IsAsciiDigit(x)))
            throw new ApiProblemException(422, "INVALID_INVOICE_FRAGMENT", "Enter invoice digits",
                "Use 4 to 12 digits from the middle of the BGL invoice number.");
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var shipments = await (from shipment in _context.CustomerShipments.AsNoTracking()
                               join client in _context.Customers.AsNoTracking()
                                   on shipment.CustomerId equals client.Id
                               where (branch.IsAllBranches || shipment.BranchId == branch.BranchId) &&
                                   shipment.InvoiceNumericFragment.Contains(digits)
                               orderby shipment.InvoiceNumericFragment == digits descending,
                                   shipment.CreatedAt descending
                               select new { Shipment = shipment, Client = client })
            .Take(20).ToListAsync(cancellationToken);

        var results = new List<object>();
        foreach (var row in shipments)
        {
            var manifest = await (from line in _context.OperationalManifestEntries.AsNoTracking()
                                  join header in _context.OperationalManifests.AsNoTracking()
                                      on line.ManifestId equals header.Id
                                  where line.CustomerShipmentId == row.Shipment.Id &&
                                      header.BranchId == row.Shipment.BranchId
                                  orderby line.AddedAt descending
                                  select header).FirstOrDefaultAsync(cancellationToken);
            var connection = manifest is null ? null :
                await _context.ManifestTrackingConnections.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ManifestId == manifest.Id, cancellationToken);
            var latest = connection is null ? null :
                await _context.ManifestTrackingEvents.AsNoTracking()
                    .Where(x => x.ConnectionId == connection.Id &&
                        x.ReviewState == TrackingEventReviewState.Published)
                    .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
                    .Select(x => new { x.PublicTitle, x.PublicDescription, x.Place,
                        x.OccurredAt, x.EstimatedAt }).FirstOrDefaultAsync(cancellationToken);
            results.Add(new
            {
                CustomerShipmentId = row.Shipment.Id,
                row.Shipment.InvoiceNumber,
                Client = new { row.Client.Id, row.Client.ClientNumber, Name = row.Client.FullName },
                row.Shipment.MovementState,
                row.Shipment.Method,
                Route = new { Origin = row.Shipment.OriginCity,
                    Destination = row.Shipment.DestinationCityOrState },
                Manifest = manifest is null ? null : new { manifest.Id, manifest.ManifestNumber,
                    ShipmentName = manifest.ShipmentId },
                Tracking = connection is null ? null : new { connection.ShippingCompanyName,
                    ExternalReference = connection.ExternalReference, connection.Status,
                    connection.LastKnownPlace, connection.EstimatedAt, LatestApprovedEvent = latest },
                CustomerShipmentRoute = $"/customer-shipments/{row.Shipment.Id}",
                ManifestRoute = manifest is null ? null : $"/operations/manifests/{manifest.Id}"
            });
        }
        return Ok(new { InvoiceFragment = digits, ActiveBranch = branch.BranchKey,
            Count = results.Count, Results = results });
    }

    private static HashSet<string> ParseTypes(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
    private static int Rank(string query, params string?[] values)
    {
        var normalized = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Normalize).ToList();
        if (normalized.Any(x => x == query)) return 0;
        if (normalized.Any(x => x.StartsWith(query, StringComparison.Ordinal))) return 1;
        return 2;
    }
    private sealed record SearchResult(string Type, Guid Id, string Reference, string Title,
        string Subtitle, string Route, int Rank);
}
