using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class ScheduledPickup : BaseEntity
{
    public Guid BranchId { get; set; }
    public string PickupNumber { get; set; } = string.Empty;
    public long RowVersion { get; set; } = 1;
    public Guid? ClientId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public ShipmentGoodsType GoodsType { get; set; }
    public string? Contents { get; set; }
    public string? HandlingNote { get; set; }
    public int? ExpectedPieceCount { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public decimal? ChargeableWeightKg { get; set; }
    public string? BoxSizesJson { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public PickupPaymentState PaymentState { get; set; }
    public decimal AmountPaid { get; set; }
    public Guid? FinancialTransactionId { get; set; }
    public Guid? ReceiptDocumentId { get; set; }
    public PickupState State { get; set; }
    public Guid? AssignedDriverUserId { get; set; }
    public string? AssignedTeam { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public string? DeleteReason { get; set; }
}
