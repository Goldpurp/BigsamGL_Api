using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A manifest is one operational shipment. Customer, invoice, package and payment details
/// belong to child entries and are never stored on this shipment header.
/// </summary>
public sealed class OperationalManifest : BaseEntity
{
    public Guid BranchId { get; set; }
    public string ManifestNumber { get; set; } = string.Empty;
    public string ShipmentId { get; set; } = string.Empty;
    public string ShipmentBookingId { get; set; } = string.Empty;
    public string? BookingReference { get; set; }
    public Guid? ServiceRouteId { get; set; }
    public string RouteCode { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string OriginCountryCode { get; set; } = string.Empty;
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCountryCode { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public DateTime ManifestDate { get; set; }
    public ManifestLogisticsMethod LogisticsMethod { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? LastEditedByUserId { get; set; }
    public string? CarrierName { get; set; }
    public string? CarrierReference { get; set; }
    public string? TrackingProvider { get; set; }
    public string? ContainerOrFlightNumber { get; set; }
    public string? BillOfLadingNumber { get; set; }
    public string? FlightNumber { get; set; }
    public string? VesselName { get; set; }
    public string? VoyageNumber { get; set; }
    public string? Notes { get; set; }
    public long RowVersion { get; set; } = 1;
}
