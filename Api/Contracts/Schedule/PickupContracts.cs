using Domain.Enums;

namespace Api.Controllers.Schedule;

public sealed record PickupBoxInput(string Code, decimal Quantity);
public sealed record PickupPaymentInput(PickupPaymentState State, decimal AmountPaid,
    PaymentMethod? Method, string? Reference);
public sealed record CreatePickupRequest(Guid? ClientId, string Name, string Phone, string? Email,
    string Address, string City, DateTime ScheduledAt, ShipmentGoodsType GoodsType, string? Contents,
    string? HandlingNote, int? ExpectedPieceCount, decimal? ActualWeightKg,
    decimal? ChargeableWeightKg, IReadOnlyList<PickupBoxInput> BoxSizes, decimal Price,
    PickupPaymentInput Payment);
public sealed record UpdatePickupRequest(string Name, string Phone, string? Email, string Address,
    string City, DateTime ScheduledAt, ShipmentGoodsType GoodsType, string? Contents,
    string? HandlingNote, int? ExpectedPieceCount, decimal? ActualWeightKg,
    decimal? ChargeableWeightKg, IReadOnlyList<PickupBoxInput> BoxSizes, string Reason);
public sealed record PickupReasonRequest(string Reason);
public sealed record AssignPickupDriverRequest(Guid? DriverUserId, string? Team, string Reason);

public sealed record PickupResponse(Guid Id, string PickupNumber, long RowVersion, Guid BranchId,
    string Branch, string Currency, Guid? ClientId, string Name, string Phone, string? Email,
    string Address, string City, DateTime ScheduledAt, string TimeZone, ShipmentGoodsType GoodsType,
    string? Contents, string? HandlingNote, int? ExpectedPieceCount, decimal? ActualWeightKg,
    decimal? ChargeableWeightKg, IReadOnlyList<PickupBoxInput> BoxSizes, decimal Price,
    PickupPaymentState PaymentState, decimal AmountPaid, PaymentMethod? PaymentMethod,
    string? PaymentReference, PickupState State, Guid? AssignedDriverUserId, string? AssignedTeam,
    DateTime? CollectedAt, DateTime? CancelledAt, DateTime? DeletedAt, DateTime CreatedAt,
    DateTime? LastModifiedAt);
