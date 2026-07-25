using Domain.Enums;

namespace Api.Controllers.Operations;

public sealed record ManifestDestinationInput(string CountryCode, string City);
public sealed record CreateManifestRequest(string ShipmentName, string? BookingReference,
    DateTime ManifestDate, CustomerShipmentMethod Method, ManifestDestinationInput Destination)
{
    public Guid? ServiceRouteId { get; init; }
}
public sealed record UpdateManifestRequest(string ShipmentName, string? BookingReference);
public sealed record AddManifestLineRequest(string InvoiceNumber, string SenderName,
    decimal? ActualWeightKg, decimal? ChargeableWeightKg);
public sealed record UpdateManifestLineRequest(string SenderName, string ReceiverName,
    string Description, string? Contents, int Pieces, decimal ActualWeightKg,
    decimal? ChargeableWeightKg, ShipmentHandoverMethod HandoverMethod,
    string DestinationCity, string? DeliveryAddress, string Reason);
public sealed record ReassignManifestLineRequest(string InvoiceNumber, bool Confirm,
    string Reason);
public sealed record UpdateManifestCarrierRequest(string CarrierName, string? CarrierReference,
    string? TrackingProvider, string? ContainerOrFlightNumber, string Reason,
    string? BillOfLadingNumber = null, string? FlightNumber = null,
    string? VesselName = null, string? VoyageNumber = null, string? Notes = null);
public sealed record EmailManifestRequest(string Subject, string Body,
    IReadOnlyList<Guid> AttachmentDocumentIds);

public sealed record ManifestLineResponse(Guid Id, long RowVersion, Guid CustomerShipmentId,
    string InvoiceNumber, Guid ClientId, string ClientNumber, string ClientName, string SenderName,
    string ReceiverName, string Description, string? Contents, int Pieces, decimal ActualWeightKg,
    decimal? ChargeableWeightKg, ShipmentHandoverMethod HandoverMethod, string DestinationCity,
    string? DeliveryAddress, DateTime AddedAt, DateTime? LastModifiedAt)
{
    public Guid ManifestId { get; init; }
    public Guid InvoiceId { get; init; }
    public string CustomerPhone { get; init; } = string.Empty;
    public string? CustomerEmail { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public PaymentMethod PaymentMethod { get; init; }
    public ShipmentChargeBasis ChargeBasis { get; init; }
    public string? NegotiationReference { get; init; }
    public string? Notes { get; init; }
    public Guid AddedByUserId { get; init; }
    public string AddedBy { get; init; } = string.Empty;
}
public sealed record ManifestResponse(Guid Id, string ManifestNumber, long RowVersion,
    Guid BranchId, string Branch, string ShipmentName, string? BookingReference,
    DateTime ManifestDate, CustomerShipmentMethod Method, string OriginCountryCode,
    string OriginCity, string DestinationCountryCode, string DestinationCity,
    string? CarrierName, string? CarrierReference, string? TrackingProvider,
    string? ContainerOrFlightNumber, int LineCount, DateTime CreatedAt,
    DateTime? LastModifiedAt, IReadOnlyList<ManifestLineResponse>? Lines)
{
    public string ShipmentBookingId { get; init; } = string.Empty;
    public Guid? ServiceRouteId { get; init; }
    public string RouteCode { get; init; } = string.Empty;
    public string RouteName { get; init; } = string.Empty;
    public Guid CreatedByUserId { get; init; }
    public string? CreatedBy { get; init; }
    public string? BillOfLadingNumber { get; init; }
    public string? FlightNumber { get; init; }
    public string? VesselName { get; init; }
    public string? VoyageNumber { get; init; }
    public string? Notes { get; init; }
}
public sealed record ManifestInvoiceLookupResponse(string InvoiceNumber, Guid CustomerShipmentId,
    Guid ClientId, string ClientNumber, string ClientName, string ReceiverName,
    CustomerShipmentMethod Method, string Description, string? Contents, int Pieces,
    decimal? ActualWeightKg, decimal? ChargeableWeightKg, ShipmentHandoverMethod HandoverMethod,
    string DestinationCountryCode, string DestinationCity, string? DeliveryAddress,
    Guid? ManifestId, string? ManifestNumber, string? ShipmentName);
public sealed record ManifestSummaryResponse(int AirManifests, int SeaManifests,
    int EmptyManifests, int AttachedShipments);
