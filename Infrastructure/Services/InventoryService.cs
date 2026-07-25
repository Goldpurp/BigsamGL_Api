using System.Data;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// The single write boundary for company-owned stock. Serializable transactions,
/// advisory locks and idempotency keys prevent double receipts and lost updates.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private static readonly InventoryTransactionType[] InboundTypes =
    [
        InventoryTransactionType.OpeningBalance,
        InventoryTransactionType.CustomerReturn,
        InventoryTransactionType.AdjustmentIncrease
    ];

    private static readonly InventoryTransactionType[] OutboundTypes =
    [
        InventoryTransactionType.Issue,
        InventoryTransactionType.AdjustmentDecrease,
        InventoryTransactionType.WriteOff
    ];

    private readonly ApplicationDbContext _context;
    private readonly ICompanyDocumentNumberGenerator _numbers;

    public InventoryService(ApplicationDbContext context, ICompanyDocumentNumberGenerator numbers)
    {
        _context = context;
        _numbers = numbers;
    }

    public async Task<InventoryTransaction> AdjustAsync(InventoryAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCommon(command.Quantity, command.Reason, command.IdempotencyKey, command.OccurredAt,
            allowZeroQuantity: command.Type == InventoryTransactionType.StockCountCorrection);
        if (!InboundTypes.Contains(command.Type) && !OutboundTypes.Contains(command.Type) &&
            command.Type != InventoryTransactionType.StockCountCorrection)
            throw new ArgumentException("This transaction type cannot be posted as an adjustment.");

        var existing = await _context.InventoryTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == command.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return existing;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var repeated = await _context.InventoryTransactions.FirstOrDefaultAsync(
                x => x.IdempotencyKey == command.IdempotencyKey.Trim(), cancellationToken);
            if (repeated is not null) return repeated;

            await LockAsync(command.InventoryItemId, command.InventoryLocationId, cancellationToken);
            var item = await RequireItemAsync(command.InventoryItemId, cancellationToken);
            var location = await RequireLocationAsync(command.InventoryLocationId, cancellationToken);
            var stock = await GetOrCreateStockAsync(item.Id, location.Id, cancellationToken);

            var requestedCost = command.UnitCost ?? item.DefaultUnitCost;
            if (requestedCost < 0) throw new ArgumentException("Unit cost cannot be negative.");
            decimal movementQuantity;
            Guid? from = null;
            Guid? to = null;

            if (command.Type == InventoryTransactionType.StockCountCorrection)
            {
                var target = command.Quantity;
                if (target < 0) throw new ArgumentException("Counted stock cannot be negative.");
                movementQuantity = Math.Abs(target - stock.QuantityOnHand);
                if (movementQuantity == 0) throw new InvalidOperationException("The stock count already matches the recorded quantity.");
                if (target > stock.QuantityOnHand)
                {
                    ApplyInbound(stock, movementQuantity, requestedCost);
                    to = location.Id;
                }
                else
                {
                    EnsureAvailable(stock, movementQuantity);
                    stock.QuantityOnHand -= movementQuantity;
                    from = location.Id;
                    requestedCost = stock.AverageUnitCost;
                }
            }
            else if (InboundTypes.Contains(command.Type))
            {
                movementQuantity = command.Quantity;
                ApplyInbound(stock, movementQuantity, requestedCost);
                to = location.Id;
            }
            else
            {
                movementQuantity = command.Quantity;
                EnsureAvailable(stock, movementQuantity);
                requestedCost = stock.AverageUnitCost;
                stock.QuantityOnHand -= movementQuantity;
                from = location.Id;
            }

            stock.LastMovementAt = command.OccurredAt.ToUniversalTime();
            var result = await CreateTransactionAsync(item.Id, command.Type, from, to,
                movementQuantity, requestedCost, "InventoryAdjustment", null, null, command.Reason,
                command.IdempotencyKey, command.OccurredAt, command.PerformedByUserId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task<InventoryTransaction> TransferAsync(InventoryTransferCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCommon(command.Quantity, command.Reason, command.IdempotencyKey, command.OccurredAt);
        if (command.FromLocationId == command.ToLocationId)
            throw new ArgumentException("Source and destination locations must differ.");
        var existing = await _context.InventoryTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == command.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return existing;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var repeated = await _context.InventoryTransactions.FirstOrDefaultAsync(
                x => x.IdempotencyKey == command.IdempotencyKey.Trim(), cancellationToken);
            if (repeated is not null) return repeated;

            var locationIds = new[] { command.FromLocationId, command.ToLocationId }.OrderBy(x => x).ToArray();
            foreach (var locationId in locationIds)
                await LockAsync(command.InventoryItemId, locationId, cancellationToken);
            var item = await RequireItemAsync(command.InventoryItemId, cancellationToken);
            await RequireLocationAsync(command.FromLocationId, cancellationToken);
            await RequireLocationAsync(command.ToLocationId, cancellationToken);
            var source = await GetOrCreateStockAsync(item.Id, command.FromLocationId, cancellationToken);
            var destination = await GetOrCreateStockAsync(item.Id, command.ToLocationId, cancellationToken);
            EnsureAvailable(source, command.Quantity);
            var cost = source.AverageUnitCost;
            source.QuantityOnHand -= command.Quantity;
            ApplyInbound(destination, command.Quantity, cost);
            source.LastMovementAt = destination.LastMovementAt = command.OccurredAt.ToUniversalTime();

            var result = await CreateTransactionAsync(item.Id, InventoryTransactionType.Transfer,
                source.InventoryLocationId, destination.InventoryLocationId, command.Quantity, cost,
                "InventoryTransfer", null, null, command.Reason, command.IdempotencyKey,
                command.OccurredAt, command.PerformedByUserId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task<IReadOnlyList<InventoryTransaction>> ReceivePurchaseAsync(PurchaseReceiptCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Lines.Count == 0) throw new ArgumentException("At least one receipt line is required.");
        ValidateCommon(1, command.Notes ?? "Purchase receipt", command.IdempotencyKey, command.OccurredAt);
        if (command.Lines.Any(x => x.Quantity <= 0))
            throw new ArgumentException("Receipt quantities must be greater than zero.");
        if (command.Lines.Select(x => x.CompanyPurchaseLineId).Distinct().Count() != command.Lines.Count)
            throw new ArgumentException("A purchase line can appear only once in a receipt.");

        var rootKey = command.IdempotencyKey.Trim();
        var existing = await _context.InventoryTransactions.AsNoTracking()
            .Where(x => x.IdempotencyKey.StartsWith(rootKey + ":"))
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        if (existing.Count > 0) return existing;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IReadOnlyList<InventoryTransaction>>(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var repeated = await _context.InventoryTransactions
                .Where(x => x.IdempotencyKey.StartsWith(rootKey + ":"))
                .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
            if (repeated.Count > 0) return repeated;

            var purchase = await _context.CompanyPurchases.FirstOrDefaultAsync(
                x => x.Id == command.CompanyPurchaseId, cancellationToken)
                ?? throw new KeyNotFoundException("Company purchase not found.");
            if (purchase.Status is not (CompanyPurchaseStatus.Approved or CompanyPurchaseStatus.PartiallyPaid or CompanyPurchaseStatus.Paid))
                throw new InvalidOperationException("Only approved purchases can be received into inventory.");

            var lineIds = command.Lines.Select(x => x.CompanyPurchaseLineId).ToArray();
            var purchaseLines = await _context.CompanyPurchaseLines
                .Where(x => lineIds.Contains(x.Id) && x.CompanyPurchaseId == purchase.Id)
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            if (purchaseLines.Count != lineIds.Length)
                throw new ArgumentException("One or more receipt lines do not belong to this purchase.");

            var results = new List<InventoryTransaction>();
            foreach (var requested in command.Lines.OrderBy(x => x.CompanyPurchaseLineId))
            {
                var line = purchaseLines[requested.CompanyPurchaseLineId];
                if (!line.InventoryItemId.HasValue)
                    throw new ArgumentException($"Purchase line {line.Id} is not linked to an inventory item.");
                if (line.QuantityReceived + requested.Quantity > line.Quantity)
                    throw new InvalidOperationException($"Receipt quantity exceeds the outstanding quantity for line {line.Id}.");

                await LockAsync(line.InventoryItemId.Value, requested.InventoryLocationId, cancellationToken);
                var item = await RequireItemAsync(line.InventoryItemId.Value, cancellationToken);
                var location = await RequireLocationAsync(requested.InventoryLocationId, cancellationToken);
                var locationBranch = await _context.Branches.AsNoTracking()
                    .Where(x => x.Id == location.BranchId).Select(x => x.Code).SingleAsync(cancellationToken);
                if (!string.Equals(locationBranch, purchase.BranchCode, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Inventory must be received into a location belonging to the purchase branch.");

                var stock = await GetOrCreateStockAsync(item.Id, location.Id, cancellationToken);
                ApplyInbound(stock, requested.Quantity, line.UnitPrice);
                stock.LastMovementAt = command.OccurredAt.ToUniversalTime();
                line.QuantityReceived += requested.Quantity;
                var reason = string.IsNullOrWhiteSpace(command.Notes)
                    ? $"Receipt for {purchase.PurchaseNumber}" : command.Notes.Trim();
                results.Add(await CreateTransactionAsync(item.Id, InventoryTransactionType.PurchaseReceipt,
                    null, location.Id, requested.Quantity, line.UnitPrice, "CompanyPurchase", purchase.Id,
                    line.Id, reason, $"{rootKey}:{line.Id:N}", command.OccurredAt,
                    command.PerformedByUserId, cancellationToken));
            }

            var allLines = await _context.CompanyPurchaseLines
                .Where(x => x.CompanyPurchaseId == purchase.Id && x.InventoryItemId != null)
                .ToListAsync(cancellationToken);
            purchase.ReceivingStatus = allLines.All(x => x.QuantityReceived >= x.Quantity)
                ? PurchaseReceivingStatus.Received : PurchaseReceivingStatus.PartiallyReceived;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return results;
        });
    }

    private async Task<InventoryItem> RequireItemAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.InventoryItems.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken)
        ?? throw new KeyNotFoundException("Active inventory item not found.");

    private async Task<InventoryLocation> RequireLocationAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.InventoryLocations.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken)
        ?? throw new KeyNotFoundException("Active inventory location not found.");

    private async Task<InventoryStockLevel> GetOrCreateStockAsync(Guid itemId, Guid locationId,
        CancellationToken cancellationToken)
    {
        var stock = await _context.InventoryStockLevels.FirstOrDefaultAsync(
            x => x.InventoryItemId == itemId && x.InventoryLocationId == locationId, cancellationToken);
        if (stock is not null) return stock;
        stock = new InventoryStockLevel { InventoryItemId = itemId, InventoryLocationId = locationId };
        _context.InventoryStockLevels.Add(stock);
        return stock;
    }

    private async Task LockAsync(Guid itemId, Guid locationId, CancellationToken cancellationToken)
    {
        var key = $"inventory:{itemId:N}:{locationId:N}";
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken);
    }

    private async Task<InventoryTransaction> CreateTransactionAsync(Guid itemId,
        InventoryTransactionType type, Guid? fromLocationId, Guid? toLocationId, decimal quantity,
        decimal unitCost, string referenceType, Guid? referenceId, Guid? purchaseLineId, string reason,
        string idempotencyKey, DateTime occurredAt, Guid performedBy, CancellationToken cancellationToken)
    {
        var entry = new InventoryTransaction
        {
            TransactionNumber = await _numbers.NextInventoryTransactionAsync(cancellationToken),
            IdempotencyKey = idempotencyKey.Trim(), InventoryItemId = itemId, Type = type,
            FromLocationId = fromLocationId, ToLocationId = toLocationId, Quantity = quantity,
            UnitCost = unitCost, TotalCost = Math.Round(quantity * unitCost, 2),
            ReferenceType = referenceType, ReferenceId = referenceId,
            CompanyPurchaseLineId = purchaseLineId, Reason = reason.Trim(),
            OccurredAt = occurredAt.ToUniversalTime(), PerformedByUserId = performedBy
        };
        _context.InventoryTransactions.Add(entry);
        return entry;
    }

    private static void ApplyInbound(InventoryStockLevel stock, decimal quantity, decimal unitCost)
    {
        var oldValue = stock.QuantityOnHand * stock.AverageUnitCost;
        var newQuantity = stock.QuantityOnHand + quantity;
        stock.AverageUnitCost = newQuantity == 0 ? 0 : Math.Round((oldValue + quantity * unitCost) / newQuantity, 4);
        stock.QuantityOnHand = newQuantity;
    }

    private static void EnsureAvailable(InventoryStockLevel stock, decimal quantity)
    {
        if (stock.QuantityAvailable < quantity)
            throw new InvalidOperationException("Insufficient available stock for this transaction.");
    }

    private static void ValidateCommon(decimal quantity, string reason, string key, DateTime occurredAt,
        bool allowZeroQuantity = false)
    {
        if (quantity < 0 || (!allowZeroQuantity && quantity == 0))
            throw new ArgumentException(allowZeroQuantity
                ? "Quantity cannot be negative." : "Quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.");
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 100)
            throw new ArgumentException("An idempotency key of at most 100 characters is required.");
        if (occurredAt == default || occurredAt.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("Movement time is invalid or in the future.");
    }
}
