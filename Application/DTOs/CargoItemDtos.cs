using Domain.Enums;

namespace Application.DTOs;

public record CustomerDto(
    Guid Id,
    string FullName,
    string Email,
    decimal WalletBalance,
    string WalletCurrency,
    string CountryCode,
    string Phone,
    string? CompanyName,
    bool IsBusiness,
    DateTime CreatedAt,
    ClientAccountType AccountType
);

public record CargoItemDto(
    Guid Id,
    Guid CustomerId,
    string TrackingNumber,
    string MarkerCode,
    string Description,
    decimal ActualWeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm,
    decimal VolumetricWeightKg,
    decimal BillableWeightKg,
    decimal DeclaredValueAmount,
    string DeclaredValueCurrency,
    string CustomsCategory,
    CargoStatus Status,
    decimal ShippingCostAmount,
    string ShippingCostCurrency,
    Guid? BatchId,
    Guid? InvoiceId,
    Guid? ShipmentMovementId,
    Guid? ShipmentContainerId,
    TransportMode? AssignedTransportMode,
    DateTime CreatedAt
);

public record ShippingBatchDto(
    Guid Id,
    string BatchCode,
    string? AirwayBillNumber,
    decimal MaxWeightCapacityKg,
    DateTime ScheduledReleaseDate,
    BatchStatus Status,
    string OriginHub,
    string DestinationHub,
    ConsolidationType ConsolidationType,
    string VesselName,
    string VoyageNumber,
    string PortOfLoading,
    string PortOfDischarge,
    bool FreezeTracking,
    DateTime CreatedAt
);

public record WalletLedgerDto(
    Guid Id,
    Guid CustomerId,
    Guid ReferenceId,
    decimal Amount,
    string Currency,
    LedgerTransactionType TransactionType,
    string Description,
    DateTime Timestamp
);

public record PublicTrackingResponse(
    string TrackingNumber,
    string MarkerCode,
    string Description,
    string Origin,
    string DestinationCountry,
    string CustomerFacingStatus,
    string PublicMilestoneDetails,
    DateTime LastUpdated,
    string CurrentLocation,
    DateTime? EstimatedDeliveryAt,
    int ProgressPercent,
    bool HasException,
    string? IssueCode,
    DeliveryMethod DeliveryMethod,
    TransportMode? TransportMode,
    string CarrierName,
    string? TransportReference,
    IReadOnlyList<PublicTrackingMilestoneDto> Milestones
);

public record PublicTrackingMilestoneDto(Guid Id, DateTime OccurredAt, string Status, ShipmentEventType EventType,
    string Title, string Description, string Location, string Source, DateTime? EstimatedDeliveryAt,
    bool IsException, string? IssueCode, bool IsCorrected);

public record ManifestLineItemDto(
    string MarkerCode,
    string RouteDetails,
    int PieceCount,
    decimal VerifiedWeightKg,
    decimal VolumeCbm
);

public record ManifestResponse(
    string BatchCode,
    string AirwayBillNumber,
    string VesselName,
    string VoyageNumber,
    string PortOfLoading,
    string PortOfDischarge,
    DateTime ScheduledReleaseDate,
    List<ManifestLineItemDto> LineItems,
    ManifestTransportDto Transport
);

public record ManifestTransportDto(
    string BigsamRole,
    string? MovementCode,
    TransportMode? Mode,
    TransportOperatorType? OperatorType,
    string? CarrierName,
    string? MasterTransportReference,
    string? FlightNumber,
    string? VesselName,
    string? VoyageNumber,
    string? OriginGateway,
    string? DestinationGateway
);
