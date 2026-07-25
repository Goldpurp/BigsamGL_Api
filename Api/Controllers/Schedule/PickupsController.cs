using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
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

namespace Api.Controllers.Schedule;

[ApiController]
[Authorize(Policy = "StaffOnly")]
[Route("api/schedule/pickups")]
public sealed class PickupsController : ApiControllerBase
{
    private const string CreateOperation = "pickup.create";
    private readonly ApplicationDbContext _context;
    private readonly IActiveBranchContext _branches;
    private readonly ICompanyDocumentNumberGenerator _numbers;
    private readonly IAccountingService _accounting;

    public PickupsController(ApplicationDbContext context, IActiveBranchContext branches,
        ICompanyDocumentNumberGenerator numbers, IAccountingService accounting)
    { _context = context; _branches = branches; _numbers = numbers; _accounting = accounting; }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search, [FromQuery] PickupState? state,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var query = _context.ScheduledPickups.AsNoTracking().Where(x => x.DeletedAt == null &&
            (branch.IsAllBranches || x.BranchId == branch.BranchId));
        if (state.HasValue) query = query.Where(x => x.State == state);
        if (from.HasValue) query = query.Where(x => x.ScheduledAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.ScheduledAt < to.Value.ToUniversalTime().AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.PickupNumber.ToLower().Contains(value) ||
                x.ContactName.ToLower().Contains(value) || x.Phone.Contains(value) ||
                (x.Email != null && x.Email.ToLower().Contains(value)) ||
                x.Address.ToLower().Contains(value) || x.City.ToLower().Contains(value));
        }
        var result = await query.OrderBy(x => x.State).ThenBy(x => x.ScheduledAt).ThenBy(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, true, cancellationToken);
        var keys = await BranchKeysAsync(result.Items.Select(x => x.BranchId), cancellationToken);
        return Ok(new PagedResponse<PickupResponse>(result.Items.Select(x => Map(x,
            keys.GetValueOrDefault(x.BranchId, "unknown"))).ToList(), result.Page, result.PageSize,
            result.HasPrevious, result.HasNext, result.Total, result.TotalPages));
    }

    [HttpPost]
    public async Task<ActionResult<PickupResponse>> Create(CreatePickupRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken);
        var key = IdempotencyKey(); Validate(request);
        var replay = await _context.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ActorUserId == CurrentUserId && x.Operation == CreateOperation && x.Key == key &&
            x.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (replay is not null)
            return Ok(Map(await ScopedAsync(replay.ResourceId, branch.BranchId!.Value, false,
                cancellationToken), branch.BranchKey!));

        var scheduled = request.ScheduledAt.ToUniversalTime();
        var normalizedPhone = ClientIdentityNormalizer.Phone(request.Phone);
        var duplicate = await _context.ScheduledPickups.AsNoTracking().AnyAsync(x =>
            x.BranchId == branch.BranchId && x.DeletedAt == null && x.State != PickupState.Cancelled &&
            x.Phone == normalizedPhone && x.Address.ToLower() == request.Address.Trim().ToLower() &&
            x.ScheduledAt >= scheduled.AddMinutes(-30) && x.ScheduledAt <= scheduled.AddMinutes(30),
            cancellationToken);
        if (duplicate) throw new ApiProblemException(409, "POSSIBLE_DUPLICATE_PICKUP",
            "A similar pickup is already booked",
            "Review the contact, address and time before creating another pickup.");
        if (request.ClientId.HasValue && !await _context.Customers.AsNoTracking().AnyAsync(x =>
            x.Id == request.ClientId && x.BranchId == branch.BranchId, cancellationToken))
            throw new ApiProblemException(404, "CLIENT_NOT_FOUND", "Client not found",
                "The selected client is not in the active branch.");

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var pickup = new ScheduledPickup
        {
            BranchId = branch.BranchId!.Value,
            PickupNumber = await _numbers.NextScheduledPickupAsync(cancellationToken),
            Currency = branch.Currency!, TimeZone = branch.TimeZone!, State = PickupState.Booked,
            ClientId = request.ClientId, Price = Money(request.Price),
            PaymentState = request.Payment.State, AmountPaid = Money(request.Payment.AmountPaid),
            PaymentMethod = request.Payment.Method
        };
        Apply(pickup, request.Name, request.Phone, request.Email, request.Address, request.City,
            scheduled, request.GoodsType, request.Contents, request.HandlingNote,
            request.ExpectedPieceCount, request.ActualWeightKg, request.ChargeableWeightKg,
            request.BoxSizes);
        if (pickup.AmountPaid > 0)
            await AddPaymentAsync(pickup, request.Payment, branch, cancellationToken);
        _context.ScheduledPickups.Add(pickup);
        _context.IdempotencyRecords.Add(new IdempotencyRecord { ActorUserId = CurrentUserId,
            Operation = CreateOperation, Key = key, ResourceType = nameof(ScheduledPickup),
            ResourceId = pickup.Id, ExpiresAt = DateTime.UtcNow.AddHours(24) });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var response = Map(pickup, branch.BranchKey!); Response.Headers.ETag = Quote(pickup.RowVersion);
        return CreatedAtAction(nameof(Get), new { pickupId = pickup.Id }, response);
    }

    [HttpGet("{pickupId:guid}")]
    public async Task<ActionResult<PickupResponse>> Get(Guid pickupId, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(false, cancellationToken);
        var pickup = await _context.ScheduledPickups.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == pickupId && x.DeletedAt == null && (branch.IsAllBranches || x.BranchId == branch.BranchId),
            cancellationToken) ?? throw new KeyNotFoundException("Pickup not found.");
        var key = (await BranchKeysAsync([pickup.BranchId], cancellationToken))[pickup.BranchId];
        Response.Headers.ETag = Quote(pickup.RowVersion); return Ok(Map(pickup, key));
    }

    [HttpPatch("{pickupId:guid}")]
    public async Task<ActionResult<PickupResponse>> Update(Guid pickupId, UpdatePickupRequest request,
        CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var version = Version();
        if (string.IsNullOrWhiteSpace(request.Reason)) throw Rule("CHANGE_REASON_REQUIRED", "A change reason is required.");
        ValidateFields(request.Name, request.Phone, request.Email, request.Address, request.City,
            request.ScheduledAt, request.ExpectedPieceCount, request.ActualWeightKg,
            request.ChargeableWeightKg, request.BoxSizes);
        var pickup = await ScopedAsync(pickupId, branch.BranchId!.Value, true, cancellationToken);
        CheckVersion(pickup.RowVersion, version);
        if (pickup.State is PickupState.Collected or PickupState.Cancelled)
            throw Rule("PICKUP_FINAL", "A collected or cancelled pickup cannot be edited.");
        Apply(pickup, request.Name, request.Phone, request.Email, request.Address, request.City,
            request.ScheduledAt.ToUniversalTime(), request.GoodsType, request.Contents,
            string.Concat(Null(request.HandlingNote), "\nEdit reason: ", request.Reason.Trim()),
            request.ExpectedPieceCount, request.ActualWeightKg, request.ChargeableWeightKg,
            request.BoxSizes);
        pickup.RowVersion++; await _context.SaveChangesAsync(cancellationToken);
        Response.Headers.ETag = Quote(pickup.RowVersion); return Ok(Map(pickup, branch.BranchKey!));
    }

    [HttpPost("{pickupId:guid}/cancel")]
    public async Task<ActionResult<PickupResponse>> Cancel(Guid pickupId, PickupReasonRequest request,
        CancellationToken cancellationToken)
    {
        var (pickup, branch) = await MutableAsync(pickupId, request.Reason, cancellationToken);
        if (pickup.State == PickupState.Collected)
            throw Rule("COLLECTED_PICKUP", "A collected pickup cannot be cancelled.");
        pickup.State = PickupState.Cancelled; pickup.CancelledAt = DateTime.UtcNow;
        pickup.CancellationReason = request.Reason.Trim(); pickup.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(Map(pickup, branch.BranchKey!));
    }

    [HttpDelete("{pickupId:guid}")]
    public async Task<IActionResult> Delete(Guid pickupId, [FromQuery] string reason,
        CancellationToken cancellationToken)
    {
        var (pickup, _) = await MutableAsync(pickupId, reason, cancellationToken);
        if (pickup.State == PickupState.Collected)
            throw Rule("COLLECTED_PICKUP", "A collected pickup cannot be removed from the active register.");
        pickup.DeletedAt = DateTime.UtcNow; pickup.DeletedByUserId = CurrentUserId;
        pickup.DeleteReason = reason.Trim(); pickup.RowVersion++;
        await _context.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPost("{pickupId:guid}/assign-driver")]
    public async Task<ActionResult<PickupResponse>> AssignDriver(Guid pickupId,
        AssignPickupDriverRequest request, CancellationToken cancellationToken)
    {
        var (pickup, branch) = await MutableAsync(pickupId, request.Reason, cancellationToken);
        if (!request.DriverUserId.HasValue && string.IsNullOrWhiteSpace(request.Team))
            throw Rule("DRIVER_REQUIRED", "Choose a driver or enter a collection team.");
        if (request.DriverUserId.HasValue && !await _context.UserAccounts.AsNoTracking().AnyAsync(x =>
            x.Id == request.DriverUserId && x.Status == AccountStatus.Active && x.Role != UserRole.Customer,
            cancellationToken)) throw Rule("DRIVER_NOT_AVAILABLE", "The selected staff driver is unavailable.");
        pickup.AssignedDriverUserId = request.DriverUserId;
        pickup.AssignedTeam = Null(request.Team); pickup.State = PickupState.DriverAssigned;
        pickup.RowVersion++; await _context.SaveChangesAsync(cancellationToken);
        return Ok(Map(pickup, branch.BranchKey!));
    }

    [HttpPost("{pickupId:guid}/mark-collected")]
    public async Task<ActionResult<PickupResponse>> MarkCollected(Guid pickupId,
        PickupReasonRequest request, CancellationToken cancellationToken)
    {
        var (pickup, branch) = await MutableAsync(pickupId, request.Reason, cancellationToken);
        if (pickup.State == PickupState.Cancelled) throw Rule("CANCELLED_PICKUP", "A cancelled pickup cannot be collected.");
        pickup.State = PickupState.Collected; pickup.CollectedAt = DateTime.UtcNow;
        pickup.HandlingNote = string.Concat(Null(pickup.HandlingNote), "\nCollection note: ", request.Reason.Trim());
        pickup.RowVersion++; await _context.SaveChangesAsync(cancellationToken);
        return Ok(Map(pickup, branch.BranchKey!));
    }

    private async Task AddPaymentAsync(ScheduledPickup pickup, PickupPaymentInput payment,
        ActiveBranchScope branch, CancellationToken cancellationToken)
    {
        var method = payment.Method!.Value;
        var reference = method == PaymentMethod.Cash ? $"CASH-{pickup.PickupNumber}" : payment.Reference!.Trim();
        var account = PaymentRouting.AccountCode(method);
        var financial = new FinancialTransaction
        {
            BranchId = branch.BranchId!.Value, TransactionNumber = await _numbers.NextTransactionAsync(cancellationToken),
            Direction = FinancialTransactionDirection.Credit, Type = FinancialTransactionType.OtherIncome,
            SourceType = nameof(ScheduledPickup), SourceId = pickup.Id, CounterpartyId = pickup.ClientId,
            CounterpartyName = pickup.ContactName, Amount = pickup.AmountPaid, Currency = branch.Currency!,
            Method = method, SettlementAccountCode = account, ExternalReference = reference,
            OccurredAt = DateTime.UtcNow, BranchCode = branch.Code!, RecordedByUserId = CurrentUserId,
            FreightClassification = FreightClassification.Other
        };
        var journal = await _accounting.PrepareJournalAsync(nameof(ScheduledPickup), pickup.Id,
            $"Pickup payment {pickup.PickupNumber}", financial.OccurredAt, financial.Currency,
            [new JournalPostingLine(account, financial.Amount, 0, reference),
             new JournalPostingLine("4100", 0, financial.Amount, pickup.PickupNumber)], cancellationToken);
        financial.JournalEntryId = journal.Id;
        var receipt = new TransactionDocument { DocumentNumber = await _numbers.NextFinancialDocumentAsync(
            FinancialDocumentType.Receipt, cancellationToken), Type = FinancialDocumentType.Receipt,
            FinancialTransactionId = financial.Id, IssuedAt = DateTime.UtcNow,
            IssuedByUserId = CurrentUserId, VerificationCode = DocumentSecurity.NewVerificationCode() };
        pickup.PaymentReference = reference; pickup.FinancialTransactionId = financial.Id;
        pickup.ReceiptDocumentId = receipt.Id;
        _context.FinancialTransactions.Add(financial); _context.TransactionDocuments.Add(receipt);
    }

    private async Task<(ScheduledPickup Pickup, ActiveBranchScope Branch)> MutableAsync(Guid id,
        string reason, CancellationToken cancellationToken)
    {
        var branch = await _branches.ResolveAsync(true, cancellationToken); var version = Version();
        if (string.IsNullOrWhiteSpace(reason)) throw Rule("CHANGE_REASON_REQUIRED", "A reason is required.");
        var pickup = await ScopedAsync(id, branch.BranchId!.Value, true, cancellationToken);
        CheckVersion(pickup.RowVersion, version); return (pickup, branch);
    }

    private async Task<ScheduledPickup> ScopedAsync(Guid id, Guid branchId, bool tracked,
        CancellationToken cancellationToken)
    {
        var query = tracked ? _context.ScheduledPickups.AsQueryable() : _context.ScheduledPickups.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id && x.BranchId == branchId && x.DeletedAt == null,
            cancellationToken) ?? throw new KeyNotFoundException("Pickup not found.");
    }

    private static void Validate(CreatePickupRequest request)
    {
        ValidateFields(request.Name, request.Phone, request.Email, request.Address, request.City,
            request.ScheduledAt, request.ExpectedPieceCount, request.ActualWeightKg,
            request.ChargeableWeightKg, request.BoxSizes);
        if (request.Price < 0 || request.Payment.AmountPaid < 0 || request.Payment.AmountPaid > request.Price)
            throw Rule("INVALID_PICKUP_PAYMENT", "Pickup price and amount paid are inconsistent.");
        var valid = request.Payment.State switch { PickupPaymentState.Paid => request.Payment.AmountPaid == request.Price,
            PickupPaymentState.PartPaid => request.Payment.AmountPaid > 0 && request.Payment.AmountPaid < request.Price,
            _ => request.Payment.AmountPaid == 0 };
        if (!valid) throw Rule("INVALID_PICKUP_PAYMENT_STATE", "Payment state does not match the amount paid.");
        if (request.Payment.AmountPaid > 0 && (!request.Payment.Method.HasValue ||
            !PaymentRouting.IsSupported(request.Payment.Method.Value)))
            throw Rule("PAYMENT_METHOD_REQUIRED", "Choose Cash, Card or BankTransfer for money received.");
        if (request.Payment.AmountPaid > 0 && request.Payment.Method is PaymentMethod.Card or PaymentMethod.BankTransfer &&
            string.IsNullOrWhiteSpace(request.Payment.Reference))
            throw Rule("PAYMENT_REFERENCE_REQUIRED", "Card and bank-transfer payments require a reference.");
    }

    private static void ValidateFields(string name, string phone, string? email, string address,
        string city, DateTime scheduledAt, int? pieces, decimal? actual, decimal? chargeable,
        IReadOnlyList<PickupBoxInput> boxes)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(city))
            throw Rule("PICKUP_DETAILS_REQUIRED", "Name, phone, pickup address and city are required.");
        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email.Trim()))
            throw Rule("INVALID_EMAIL", "Email must be valid when supplied.");
        if (scheduledAt == default) throw Rule("PICKUP_TIME_REQUIRED", "Choose a pickup date and time.");
        if (pieces <= 0 || actual <= 0 || chargeable <= 0 ||
            actual.HasValue && chargeable.HasValue && chargeable < actual)
            throw Rule("INVALID_PICKUP_MEASUREMENTS", "Optional pieces and weights must be positive; chargeable weight cannot be lower than actual.");
        if (boxes is null || boxes.Any(x => string.IsNullOrWhiteSpace(x.Code) || x.Quantity <= 0))
            throw Rule("INVALID_BOX_SIZES", "Every box-size row requires a code and positive quantity.");
    }

    private static void Apply(ScheduledPickup x, string name, string phone, string? email,
        string address, string city, DateTime scheduled, ShipmentGoodsType goodsType, string? contents,
        string? note, int? pieces, decimal? actual, decimal? chargeable, IReadOnlyList<PickupBoxInput> boxes)
    {
        x.ContactName = PersonName(name); x.Phone = ClientIdentityNormalizer.Phone(phone)!;
        x.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        x.Address = address.Trim(); x.City = city.Trim(); x.ScheduledAt = scheduled;
        x.GoodsType = goodsType; x.Contents = Null(contents); x.HandlingNote = Null(note);
        x.ExpectedPieceCount = pieces; x.ActualWeightKg = actual; x.ChargeableWeightKg = chargeable;
        x.BoxSizesJson = boxes.Count == 0 ? null : JsonSerializer.Serialize(boxes);
    }

    private static PickupResponse Map(ScheduledPickup x, string branch) => new(x.Id, x.PickupNumber,
        x.RowVersion, x.BranchId, branch, x.Currency, x.ClientId, x.ContactName, x.Phone, x.Email,
        x.Address, x.City, x.ScheduledAt, x.TimeZone, x.GoodsType, x.Contents, x.HandlingNote,
        x.ExpectedPieceCount, x.ActualWeightKg, x.ChargeableWeightKg,
        string.IsNullOrWhiteSpace(x.BoxSizesJson) ? [] : JsonSerializer.Deserialize<List<PickupBoxInput>>(x.BoxSizesJson) ?? [],
        x.Price, x.PaymentState, x.AmountPaid, x.PaymentMethod, x.PaymentReference, x.State,
        x.AssignedDriverUserId, x.AssignedTeam, x.CollectedAt, x.CancelledAt, x.DeletedAt,
        x.CreatedAt, x.LastModifiedAt);

    private async Task<Dictionary<Guid, string>> BranchKeysAsync(IEnumerable<Guid> ids,
        CancellationToken cancellationToken) => (await _context.Branches.AsNoTracking()
        .Where(x => ids.Distinct().Contains(x.Id)).Select(x => new { x.Id, x.Code }).ToListAsync(cancellationToken))
        .ToDictionary(x => x.Id, x => x.Code switch { "UK-MAN" => "manchester", "NG-LOS" => "lagos",
            "CN-GZ" => "guangzhou", _ => x.Code.ToLowerInvariant() });

    private Guid IdempotencyKey() => Guid.TryParse(Request.Headers["Idempotency-Key"].FirstOrDefault(), out var key)
        ? key : throw new ApiProblemException(400, "IDEMPOTENCY_KEY_REQUIRED", "Idempotency key required",
            "Send a UUID Idempotency-Key header.");
    private long Version() => long.TryParse(Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"'), out var value)
        ? value : throw new ApiProblemException(428, "IF_MATCH_REQUIRED", "Version required",
            "Send the current row version in If-Match.");
    private static void CheckVersion(long current, long supplied)
    { if (current != supplied) throw new ApiProblemException(412, "PRECONDITION_FAILED", "The pickup changed",
        "Reload the current pickup before saving."); }
    private static string Quote(long value) => $"\"{value}\"";
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string PersonName(string value) => string.Join(' ', value.Trim().Split((char[]?)null,
        StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant()));
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ApiProblemException Rule(string code, string detail) =>
        new(422, code, "Pickup rule was not satisfied", detail);
}
