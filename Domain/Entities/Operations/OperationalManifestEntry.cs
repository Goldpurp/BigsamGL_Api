using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// One customer/package row inside a shipment manifest. The row owns its invoice, financial,
/// sender, handover and later carrier/container placement snapshots.
/// </summary>
public sealed class OperationalManifestEntry : BaseEntity
{
    public Guid ManifestId { get; set; }
    public Guid? CustomerShipmentId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid? ReferrerCustomerId { get; set; }
    public Guid? CargoItemId { get; set; }
    public Guid? ShippingBatchId { get; set; }
    public Guid? ShipmentMovementId { get; set; }
    public Guid? ShipmentContainerId { get; set; }
    public string InvoiceNumberSnapshot { get; set; } = string.Empty;
    public string? ReferrerNameSnapshot { get; set; }
    public int ReferralPointsAwarded { get; set; }
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerPhoneSnapshot { get; set; } = string.Empty;
    public string CustomerEmailSnapshot { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string? ReceiverPhone { get; set; }
    public string? ReceiverEmail { get; set; }
    public string? TrackingNumberSnapshot { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string PackageDescription { get; set; } = string.Empty;
    public string? Contents { get; set; }
    public decimal Quantity { get; set; }
    public decimal ActualWeightKg { get; set; }
    public decimal CostWeightKg { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentMethod PaymentType { get; set; }
    public ShipmentChargeBasis ChargeBasis { get; set; }
    public string? NegotiationReference { get; set; }
    public ManifestHandoverMethod HandoverMethod { get; set; }
    public string? HandoverAddress { get; set; }
    public string? HandoverCity { get; set; }
    public string? MovementCodeSnapshot { get; set; }
    public string? ContainerNumberSnapshot { get; set; }
    public string? Notes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public Guid AddedByUserId { get; set; }
    public long RowVersion { get; set; } = 1;
}
