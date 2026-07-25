using Domain.Enums;

// Keep the public contract namespace stable while physically organizing contracts by module.
namespace Api.Controllers;

public record CreateShippingQuoteRequest(Guid? CustomerId, Guid ServiceRouteId, Guid? SalesLeadId, string ContactName,
    string ContactEmail, string ContactPhone, string CargoDescription, string CargoCategory, decimal ActualWeightKg,
    decimal LengthCm, decimal WidthCm, decimal HeightCm, decimal DeclaredValue);
public record CreateAddressRequest(AddressType Type, string ContactName, string Phone, string Line1, string? Line2,
    string City, string? State, string PostalCode, string CountryCode, bool IsDefault);
public record CreateMilestoneRequest(CargoStatus Status, ShipmentEventType EventType, string Title, string Description,
    string Location, DateTime OccurredAt, MilestoneVisibility Visibility, DateTime? EstimatedDeliveryAt,
    bool NotifyCustomer, bool IsException, string? IssueCode);
public record CorrectMilestoneRequest(CargoStatus Status, ShipmentEventType EventType, string Title, string Description,
    string Location, DateTime OccurredAt, MilestoneVisibility Visibility, DateTime? EstimatedDeliveryAt,
    bool NotifyCustomer, bool IsException, string? IssueCode, string RevisionReason);
public record BatchShipmentUpdateRequest(CargoStatus Status, ShipmentEventType EventType, string Title,
    string Description, string Location, DateTime OccurredAt, MilestoneVisibility Visibility,
    DateTime? EstimatedDeliveryAt, bool NotifyClients, bool IsException, string? IssueCode);
public record BindCarrierRequest(string Provider, string ExternalTrackingNumber);
public record CarrierWebhookRequest(string ExternalTrackingNumber, string ExternalEventId, string Status,
    string Title, string Description, string Location, DateTime OccurredAt, DateTime? EstimatedDeliveryAt,
    bool IsException = false, string? IssueCode = null);
public record ShipmentClientSummary(Guid CustomerId, string Name, string Phone, string? Email,
    IReadOnlyList<string> TrackingNumbers);
public record CreateShipmentContainerRequest(string ContainerNumber, Guid? ShippingBatchId,
    string? CarrierBookingReference, string? BillOfLadingNumber, bool LinkAllBatchCargo,
    string Provider = "MSC", ContainerType ContainerType = ContainerType.NotSpecified,
    Guid? ShipmentMovementId = null, string? DisplayName = null);
public record UpdateShipmentContainerRequest(string ContainerNumber, string Provider,
    ContainerType ContainerType, Guid? ShippingBatchId, Guid? ShipmentMovementId,
    string? CarrierBookingReference, string? BillOfLadingNumber, string? DisplayName,
    bool IsActive);
public record LinkContainerCargoRequest(Guid CargoId);
public record CreateContainerEventRequest(string EventClassifier, string EventTypeCode, string Description,
    string Location, DateTime OccurredAt, DateTime? EstimatedArrivalAt, bool NotifyClients);
public record RegisterDocumentRequest(Guid? CustomerId, Guid? CargoItemId, DocumentType Type, string FileName,
    string ContentType, long SizeBytes);
public record CreateBranchRequest(string Code, string Name, string CountryCode, string TimeZone, string Address);
public record CreateServiceRouteRequest(string Code, string Name, Guid OriginBranchId, Guid DestinationBranchId,
    TransportMode Mode, int TransitDaysMin, int TransitDaysMax, DayOfWeek? WeeklyDepartureDay, decimal VolumetricDivisor);
public record CreateRateCardRequest(Guid ServiceRouteId, string CargoCategory, decimal MinimumWeightKg, decimal RatePerKg,
    decimal MinimumCharge, decimal FuelSurchargeRate, decimal CustomsRate, string Currency, DateTime EffectiveFrom, DateTime? EffectiveTo);
public record ReplaceRateCardRequest(decimal MinimumWeightKg, decimal RatePerKg, decimal MinimumCharge,
    decimal FuelSurchargeRate, decimal CustomsRate, string Currency, DateTime EffectiveFrom);
