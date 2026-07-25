using Domain.Enums;

namespace Api.Controllers;

public sealed record CreateOperationalManifestRequest(
    string ShipmentId,
    string ShipmentBookingId,
    Guid ServiceRouteId,
    DateTime ManifestDate,
    ManifestLogisticsMethod LogisticsMethod);

/// <summary>Only the shipment identity fields are editable after manifest creation.</summary>
public sealed record UpdateOperationalManifestRequest(string ShipmentId, string ShipmentBookingId);

public sealed record ManifestCurrencyAmountResponse(string Currency, decimal Amount);

public sealed record OperationalManifestResponse(
    Guid Id,
    string ManifestNumber,
    string ShipmentId,
    string ShipmentBookingId,
    Guid? ServiceRouteId,
    string RouteCode,
    string RouteName,
    string OriginCountryCode,
    string DestinationCountryCode,
    DateTime ManifestDate,
    ManifestLogisticsMethod LogisticsMethod,
    int EntryCount,
    int CustomerCount,
    int PackageCount,
    decimal ActualWeightKg,
    decimal CostWeightKg,
    IReadOnlyList<ManifestCurrencyAmountResponse> AmountsByCurrency,
    DateTime CreatedAt,
    DateTime? LastModifiedAt);

/// <summary>
/// Customer identity entered in the package modal. ExistingCustomerId is populated only when
/// the user selects one of the live name suggestions. New customers require phone, email and country.
/// </summary>
public sealed record ManifestEntryCustomerInput(
    string Name,
    Guid? ExistingCustomerId,
    string? Phone,
    string? Email,
    string? CountryCode,
    Guid? DuplicateReviewId);

/// <summary>
/// The person receiving or collecting the package. Only the receiver name is required for
/// BiGSAM handover; phone and email are optional evidence when supplied.
/// </summary>
public sealed record ManifestEntryReceiverInput(string Name, string? Phone, string? Email);

/// <summary>The only user-selected invoice field. Amount is verified against the pricing breakdown.</summary>
public sealed record ManifestEntryInvoiceInput(decimal Amount, PaymentMethod PaymentMethod);

/// <summary>
/// One immutable commercial calculation line. Air lines use dimensions, actual weight and a
/// rate per billable kg. Sea lines use box/item quantity and unit price. Negotiated lines retain
/// the agreed description, quantity and price without forcing them into a standard tariff.
/// </summary>
public sealed record ManifestEntryChargeLineInput(
    ShipmentChargeLineType Type,
    string Description,
    string? BoxSize,
    decimal Quantity,
    decimal UnitPrice,
    decimal? LengthCm,
    decimal? WidthCm,
    decimal? HeightCm,
    decimal? ActualWeightKg,
    decimal? VolumetricDivisor);

public sealed record ManifestEntryPricingInput(
    ShipmentChargeBasis ChargeBasis,
    string? NegotiationReference,
    IReadOnlyList<ManifestEntryChargeLineInput> Lines);

public sealed record CreateOperationalManifestEntryRequest(
    ManifestEntryCustomerInput Customer,
    ManifestEntryReceiverInput Receiver,
    ManifestEntryInvoiceInput Invoice,
    ManifestEntryPricingInput Pricing,
    Guid? ReferrerCustomerId,
    Guid? CargoItemId,
    Guid? ShippingBatchId,
    Guid? ShipmentMovementId,
    Guid? ShipmentContainerId,
    string SenderName,
    string PackageDescription,
    decimal Quantity,
    decimal ActualWeightKg,
    decimal CostWeightKg,
    ManifestHandoverMethod HandoverMethod,
    string? HandoverAddress,
    string? HandoverCity,
    string? Notes);

/// <summary>
/// Operational package fields remain editable. Customer and invoice details are intentionally absent
/// because the generated invoice is an immutable commercial record for this shipment entry.
/// </summary>
public sealed record UpdateOperationalManifestEntryRequest(
    Guid? CargoItemId,
    Guid? ShippingBatchId,
    Guid? ShipmentMovementId,
    Guid? ShipmentContainerId,
    ManifestEntryReceiverInput Receiver,
    string SenderName,
    string PackageDescription,
    decimal Quantity,
    decimal ActualWeightKg,
    decimal CostWeightKg,
    ManifestHandoverMethod HandoverMethod,
    string? HandoverAddress,
    string? HandoverCity,
    string? Notes);

public sealed record ManifestCustomerSuggestionResponse(
    Guid CustomerId,
    string Name,
    string Phone,
    string Email,
    string CountryCode,
    bool ExactNameMatch);

public sealed record OperationalManifestEntryResponse(
    Guid Id,
    Guid ManifestId,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    string ReceiverName,
    string? ReceiverPhone,
    string? ReceiverEmail,
    Guid InvoiceId,
    string InvoiceNumber,
    bool IsInvoiced,
    Guid? ReferrerCustomerId,
    string? ReferrerName,
    int ReferralPointsAwarded,
    Guid? CargoItemId,
    string? TrackingNumber,
    string SenderName,
    string PackageDescription,
    decimal Quantity,
    decimal ActualWeightKg,
    decimal CostWeightKg,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentType,
    ShipmentChargeBasis ChargeBasis,
    string? NegotiationReference,
    ManifestHandoverMethod HandoverMethod,
    string HandoverLabel,
    string? HandoverAddress,
    string? HandoverCity,
    Guid? ShippingBatchId,
    string? BatchCode,
    Guid? ShipmentMovementId,
    string? MovementCode,
    Guid? ShipmentContainerId,
    string? ContainerNumber,
    string? ContainerName,
    string? Notes,
    DateTime AddedAt,
    DateTime? LastModifiedAt);

public sealed record OperationalManifestChargeLineResponse(
    Guid Id,
    ShipmentChargeLineType Type,
    string Description,
    string? BoxSize,
    decimal Quantity,
    decimal UnitPrice,
    decimal? LengthCm,
    decimal? WidthCm,
    decimal? HeightCm,
    decimal? ActualWeightKg,
    decimal? VolumetricDivisor,
    decimal? VolumetricWeightKg,
    decimal? BillableWeightKg,
    decimal LineAmount,
    int SortOrder);

public sealed record OperationalManifestPricingResponse(
    Guid ManifestEntryId,
    ShipmentChargeBasis ChargeBasis,
    string? NegotiationReference,
    string Currency,
    decimal TotalAmount,
    decimal ActualWeightKg,
    decimal CostWeightKg,
    IReadOnlyList<OperationalManifestChargeLineResponse> Lines);

public sealed record OperationalManifestInvoiceLineResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal LineTotal);

/// <summary>Read-only invoice projection for the separate invoice panel in the package modal.</summary>
public sealed record OperationalManifestInvoiceResponse(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string IssuerBranchCode,
    DateTime IssueDate,
    DateTime DueDate,
    InvoiceStatus Status,
    InvoicePaymentStatus PaymentStatus,
    PaymentMethod? PreferredPaymentMethod,
    string Currency,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue,
    Guid? PaymentId,
    string? PaymentReference,
    DateTime? PaidAt,
    Guid? ReceiptId,
    string? ReceiptNumber,
    bool IsReadOnly,
    IReadOnlyList<OperationalManifestInvoiceLineResponse> Lines);

public sealed record VerifyManifestEntryReleaseRequest(string InvoiceNumber);
