using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "InventoryTeam")]
[Route("api/inventory")]
public sealed class InventoryController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IInventoryService _inventory;

    public InventoryController(IApplicationDbContext context, IInventoryService inventory)
    {
        _context = context;
        _inventory = inventory;
    }

    [HttpPost("items")]
    public async Task<ActionResult> CreateItem(CreateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateItemAsync(request.Name, request.Description, request.Category,
            request.UnitOfMeasure, request.DefaultUnitCost, request.Currency,
            request.PreferredSupplierId, cancellationToken);
        var sku = RequireCode(request.Sku, "SKU", 60);
        var barcode = CleanOptional(request.Barcode);
        if (await _context.InventoryItems.AnyAsync(x => x.Sku == sku ||
            (barcode != null && x.Barcode == barcode), cancellationToken))
            return Conflict("An inventory item already uses this SKU or barcode.");
        var item = new InventoryItem
        {
            Sku = sku, Barcode = barcode, Name = request.Name.Trim(),
            Description = request.Description.Trim(), Category = request.Category.Trim(),
            Type = request.Type, UnitOfMeasure = request.UnitOfMeasure.Trim(),
            DefaultUnitCost = request.DefaultUnitCost,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            PreferredSupplierId = request.PreferredSupplierId,
            IsLotTracked = request.IsLotTracked
        };
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
    }

    [HttpPut("items/{id:guid}")]
    public async Task<ActionResult> UpdateItem(Guid id, UpdateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Inventory item not found.");
        await ValidateItemAsync(request.Name, request.Description, request.Category,
            request.UnitOfMeasure, request.DefaultUnitCost, request.Currency,
            request.PreferredSupplierId, cancellationToken);
        var barcode = CleanOptional(request.Barcode);
        if (barcode is not null && await _context.InventoryItems.AnyAsync(
            x => x.Id != id && x.Barcode == barcode, cancellationToken))
            return Conflict("Another inventory item already uses this barcode.");
        if (!request.IsActive && await _context.InventoryStockLevels.AnyAsync(
            x => x.InventoryItemId == id && x.QuantityOnHand > 0, cancellationToken))
            return Conflict("An item with stock on hand cannot be deactivated.");
        item.Barcode = barcode; item.Name = request.Name.Trim();
        item.Description = request.Description.Trim(); item.Category = request.Category.Trim();
        item.Type = request.Type; item.UnitOfMeasure = request.UnitOfMeasure.Trim();
        item.DefaultUnitCost = request.DefaultUnitCost;
        item.Currency = request.Currency.Trim().ToUpperInvariant();
        item.PreferredSupplierId = request.PreferredSupplierId;
        item.IsLotTracked = request.IsLotTracked; item.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(item);
    }

    [HttpGet("items/{id:guid}")]
    public async Task<ActionResult> GetItem(Guid id, CancellationToken cancellationToken)
    {
        var item = await _context.InventoryItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken);
        if (item is null) return NotFound();
        var stock = await StockQuery().Where(x => x.ItemId == id).ToListAsync(cancellationToken);
        return Ok(new { Item = item, Stock = stock });
    }

    [HttpGet("items")]
    public async Task<ActionResult> GetItems([FromQuery] InventoryItemType? type,
        [FromQuery] string? category, [FromQuery] bool? active, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryItems.AsNoTracking().AsQueryable();
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var value = category.Trim().ToLowerInvariant();
            query = query.Where(x => x.Category.ToLower() == value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Sku.ToLower().Contains(value) || x.Name.ToLower().Contains(value) ||
                (x.Barcode != null && x.Barcode.ToLower().Contains(value)) || x.Category.ToLower().Contains(value));
        }
        return Ok(await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpPost("locations")]
    public async Task<ActionResult> CreateLocation(CreateInventoryLocationRequest request,
        CancellationToken cancellationToken)
    {
        var code = RequireCode(request.Code, "Location code", 50);
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Location name is required.");
        if (!await _context.Branches.AnyAsync(x => x.Id == request.BranchId && x.IsActive, cancellationToken))
            return BadRequest("Active branch was not found.");
        if (await _context.InventoryLocations.AnyAsync(x => x.Code == code, cancellationToken))
            return Conflict("An inventory location already uses this code.");
        var location = new InventoryLocation
        {
            BranchId = request.BranchId, Code = code, Name = request.Name.Trim(), Type = request.Type,
            AddressOrArea = request.AddressOrArea.Trim()
        };
        _context.InventoryLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/inventory/locations/{location.Id}", location);
    }

    [HttpPut("locations/{id:guid}")]
    public async Task<ActionResult> UpdateLocation(Guid id, UpdateInventoryLocationRequest request,
        CancellationToken cancellationToken)
    {
        var location = await _context.InventoryLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Inventory location not found.");
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Location name is required.");
        if (!request.IsActive && await _context.InventoryStockLevels.AnyAsync(
            x => x.InventoryLocationId == id && x.QuantityOnHand > 0, cancellationToken))
            return Conflict("A location with stock on hand cannot be deactivated.");
        location.Name = request.Name.Trim(); location.Type = request.Type;
        location.AddressOrArea = request.AddressOrArea.Trim(); location.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(location);
    }

    [HttpGet("locations")]
    public async Task<ActionResult> GetLocations([FromQuery] Guid? branchId, [FromQuery] bool? active,
        CancellationToken cancellationToken)
    {
        var query = from location in _context.InventoryLocations.AsNoTracking()
                    join branch in _context.Branches.AsNoTracking() on location.BranchId equals branch.Id
                    select new { location.Id, location.BranchId, BranchCode = branch.Code, location.Code,
                        location.Name, location.Type, location.AddressOrArea, location.IsActive };
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        return Ok(await query.OrderBy(x => x.BranchCode).ThenBy(x => x.Name).ToListAsync(cancellationToken));
    }

    [HttpGet("stock")]
    public async Task<ActionResult> GetStock([FromQuery] Guid? itemId, [FromQuery] Guid? locationId,
        [FromQuery] Guid? branchId, [FromQuery] bool? lowStock, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = StockQuery();
        if (itemId.HasValue) query = query.Where(x => x.ItemId == itemId);
        if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        if (lowStock == true) query = query.Where(x => x.QuantityAvailable <= x.ReorderLevel);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Sku.ToLower().Contains(value) || x.ItemName.ToLower().Contains(value) ||
                x.LocationName.ToLower().Contains(value));
        }
        return Ok(await query.OrderBy(x => x.ItemName).ThenBy(x => x.LocationName)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpPatch("stock/{id:guid}/policy")]
    public async Task<ActionResult> UpdateStockPolicy(Guid id, UpdateStockPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (request.QuantityReserved < 0 || request.ReorderLevel < 0 || request.TargetStockLevel < 0)
            return BadRequest("Stock policy values cannot be negative.");
        var stock = await _context.InventoryStockLevels.FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken) ?? throw new KeyNotFoundException("Stock level not found.");
        if (request.QuantityReserved > stock.QuantityOnHand)
            return Conflict("Reserved quantity cannot exceed stock on hand.");
        stock.QuantityReserved = request.QuantityReserved;
        stock.ReorderLevel = request.ReorderLevel;
        stock.TargetStockLevel = request.TargetStockLevel;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(stock);
    }

    [HttpGet("basic-stock")]
    public async Task<ActionResult> BasicStock([FromQuery] string? search,
        [FromQuery] bool? lowStock, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = from item in _context.InventoryItems.AsNoTracking()
                    join stock in _context.InventoryStockLevels.AsNoTracking()
                        on item.Id equals stock.InventoryItemId into balances
                    where item.IsActive
                    select new BasicStockRow
                    {
                        ItemId = item.Id, Sku = item.Sku, Barcode = item.Barcode,
                        Name = item.Name, Category = item.Category,
                        UnitOfMeasure = item.UnitOfMeasure,
                        QuantityOnHand = balances.Sum(x => (decimal?)x.QuantityOnHand) ?? 0,
                        ReorderLevel = balances.Sum(x => (decimal?)x.ReorderLevel) ?? 0
                    };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Sku.ToLower().Contains(value) || x.Name.ToLower().Contains(value) ||
                (x.Barcode != null && x.Barcode.ToLower().Contains(value)) || x.Category.ToLower().Contains(value));
        }
        if (lowStock == true) query = query.Where(x => x.QuantityOnHand <= x.ReorderLevel);
        return Ok(await query.OrderBy(x => x.Name).ThenBy(x => x.ItemId)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpPost("stock-in")]
    public async Task<ActionResult> StockIn(BasicStockInRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventory.AdjustAsync(new InventoryAdjustmentCommand(
            request.InventoryItemId, request.InventoryLocationId,
            InventoryTransactionType.AdjustmentIncrease, request.Quantity, request.UnitCost,
            request.Reason, request.IdempotencyKey, request.OccurredAt, CurrentUserId), cancellationToken);
        return Ok(new { Movement = "StockIn", Transaction = result });
    }

    [HttpPost("stock-out")]
    public async Task<ActionResult> StockOut(BasicStockOutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventory.AdjustAsync(new InventoryAdjustmentCommand(
            request.InventoryItemId, request.InventoryLocationId,
            InventoryTransactionType.Issue, request.Quantity, null,
            request.Reason, request.IdempotencyKey, request.OccurredAt, CurrentUserId), cancellationToken);
        return Ok(new { Movement = "StockOut", Transaction = result });
    }

    [HttpPost("adjustments")]
    public async Task<ActionResult> Adjust(InventoryAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventory.AdjustAsync(new InventoryAdjustmentCommand(
            request.InventoryItemId, request.InventoryLocationId, request.Type, request.Quantity,
            request.UnitCost, request.Reason, request.IdempotencyKey, request.OccurredAt,
            CurrentUserId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("transfers")]
    public async Task<ActionResult> Transfer(InventoryTransferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventory.TransferAsync(new InventoryTransferCommand(
            request.InventoryItemId, request.FromLocationId, request.ToLocationId, request.Quantity,
            request.Reason, request.IdempotencyKey, request.OccurredAt, CurrentUserId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("transactions")]
    public async Task<ActionResult> GetTransactions([FromQuery] Guid? itemId,
        [FromQuery] Guid? locationId, [FromQuery] InventoryTransactionType? type,
        [FromQuery] string? referenceType, [FromQuery] Guid? referenceId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryTransactions.AsNoTracking().AsQueryable();
        if (itemId.HasValue) query = query.Where(x => x.InventoryItemId == itemId);
        if (locationId.HasValue) query = query.Where(x => x.FromLocationId == locationId || x.ToLocationId == locationId);
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(referenceType))
        {
            var value = referenceType.Trim().ToLowerInvariant();
            query = query.Where(x => x.ReferenceType.ToLower() == value);
        }
        if (referenceId.HasValue) query = query.Where(x => x.ReferenceId == referenceId);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to.Value.ToUniversalTime());
        return Ok(await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("summary")]
    public async Task<ActionResult> Summary([FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var stockQuery = from stock in _context.InventoryStockLevels.AsNoTracking()
                         join location in _context.InventoryLocations.AsNoTracking()
                             on stock.InventoryLocationId equals location.Id
                         where !branchId.HasValue || location.BranchId == branchId.Value
                         select stock;
        var itemCount = await stockQuery.Select(x => x.InventoryItemId).Distinct().CountAsync(cancellationToken);
        var locationCount = await stockQuery.Select(x => x.InventoryLocationId).Distinct().CountAsync(cancellationToken);
        var totalUnits = await stockQuery.SumAsync(x => (decimal?)x.QuantityOnHand, cancellationToken) ?? 0;
        var reservedUnits = await stockQuery.SumAsync(x => (decimal?)x.QuantityReserved, cancellationToken) ?? 0;
        var lowStockLines = await stockQuery.CountAsync(
            x => x.QuantityOnHand - x.QuantityReserved <= x.ReorderLevel, cancellationToken);
        return Ok(new
        {
            ItemCount = itemCount, LocationCount = locationCount, TotalUnits = totalUnits,
            ReservedUnits = reservedUnits, LowStockLines = lowStockLines
        });
    }

    private IQueryable<StockRow> StockQuery() =>
        from stock in _context.InventoryStockLevels.AsNoTracking()
        join item in _context.InventoryItems.AsNoTracking() on stock.InventoryItemId equals item.Id
        join location in _context.InventoryLocations.AsNoTracking() on stock.InventoryLocationId equals location.Id
        join branch in _context.Branches.AsNoTracking() on location.BranchId equals branch.Id
        select new StockRow
        {
            Id = stock.Id, ItemId = item.Id, Sku = item.Sku, ItemName = item.Name,
            Category = item.Category, Currency = item.Currency, LocationId = location.Id,
            LocationName = location.Name, BranchId = branch.Id, BranchCode = branch.Code,
            QuantityOnHand = stock.QuantityOnHand, QuantityReserved = stock.QuantityReserved,
            QuantityAvailable = stock.QuantityOnHand - stock.QuantityReserved,
            AverageUnitCost = stock.AverageUnitCost, ReorderLevel = stock.ReorderLevel,
            TargetStockLevel = stock.TargetStockLevel, LastMovementAt = stock.LastMovementAt
        };

    private async Task ValidateItemAsync(string name, string description, string category, string unit,
        decimal cost, string currency, Guid? supplierId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description) ||
            string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Name, description, category and unit of measure are required.");
        if (cost < 0) throw new ArgumentException("Default unit cost cannot be negative.");
        if (currency.Trim().Length != 3) throw new ArgumentException("Currency must be a three-letter code.");
        if (supplierId.HasValue && !await _context.Suppliers.AnyAsync(
            x => x.Id == supplierId && x.IsActive, cancellationToken))
            throw new ArgumentException("Preferred active supplier was not found.");
    }

    private static string RequireCode(string value, string field, int maxLength)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is 0 || normalized.Length > maxLength ||
            normalized.Any(x => !char.IsLetterOrDigit(x) && x is not '-' and not '_'))
            throw new ArgumentException($"{field} must contain only letters, numbers, hyphens or underscores.");
        return normalized;
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class StockRow
    {
        public Guid Id { get; init; }
        public Guid ItemId { get; init; }
        public string Sku { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;
        public Guid LocationId { get; init; }
        public string LocationName { get; init; } = string.Empty;
        public Guid BranchId { get; init; }
        public string BranchCode { get; init; } = string.Empty;
        public decimal QuantityOnHand { get; init; }
        public decimal QuantityReserved { get; init; }
        public decimal QuantityAvailable { get; init; }
        public decimal AverageUnitCost { get; init; }
        public decimal ReorderLevel { get; init; }
        public decimal TargetStockLevel { get; init; }
        public DateTime? LastMovementAt { get; init; }
    }

    private sealed class BasicStockRow
    {
        public Guid ItemId { get; init; }
        public string Sku { get; init; } = string.Empty;
        public string? Barcode { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string UnitOfMeasure { get; init; } = string.Empty;
        public decimal QuantityOnHand { get; init; }
        public decimal ReorderLevel { get; init; }
    }
}
