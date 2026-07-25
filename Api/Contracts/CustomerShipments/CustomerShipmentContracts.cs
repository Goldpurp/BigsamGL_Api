using Domain.Enums;

namespace Api.Controllers;

public sealed record CustomerShipmentClientInput(Guid? ExistingClientId, string Name, string Phone,
    string? Email, Guid? DuplicateReviewId, string? PossibleMatchOverrideReason);
public sealed record CustomerShipmentDestinationInput(string CountryCode, string CityOrState, string? Address);
public sealed record CustomerShipmentCatalogueSelection(Guid CataloguePriceId, decimal Quantity);
public sealed record CustomerShipmentPricingInput(CustomerShipmentPricingMethod Method,
    decimal? ActualWeightKg, decimal? ChargeableWeightKg,
    IReadOnlyList<CustomerShipmentCatalogueSelection> BoxItems,
    decimal? AgreedAmount, string? NegotiatedReason, string? ServiceCode,
    decimal? LengthCm, decimal? WidthCm, decimal? HeightCm);
public sealed record CustomerShipmentInvoiceLineInput(string Description, decimal Quantity,
    string Unit, decimal UnitPrice, decimal TaxRate);
public sealed record CustomerShipmentPaymentInput(ShipmentPaymentState State, decimal AmountPaid,
    PaymentMethod? Method, string? Reference);
public sealed record CustomerCreditApplicationInput(Guid CreditNoteId, decimal Amount);

public enum ShipmentPaymentState { Paid = 0, Partial = 1, Unpaid = 2 }

public sealed record CreateCustomerShipmentRequest(
    CustomerShipmentClientInput Client,
    string ReceiverName,
    CustomerShipmentMethod Method,
    CustomerShipmentDestinationInput Destination,
    ShipmentHandoverMethod HandoverMethod,
    ShipmentGoodsType GoodsType,
    string Description,
    string? Contents,
    int PieceCount,
    CustomerShipmentPricingInput Pricing,
    IReadOnlyList<CustomerShipmentInvoiceLineInput> InvoiceLines,
    CustomerShipmentPaymentInput Payment,
    IReadOnlyList<CustomerCreditApplicationInput> CreditApplications,
    Guid? ReferredByClientId,
    string? CustomerNote);

public sealed record UpdateCustomerShipmentRequest(string ReceiverName,
    CustomerShipmentDestinationInput Destination, ShipmentHandoverMethod HandoverMethod,
    ShipmentGoodsType GoodsType, string Description, string? Contents, int PieceCount,
    string? CustomerNote, string Reason);

public sealed record CustomerShipmentPriceComponentResponse(Guid Id, Guid? CataloguePriceId,
    ShipmentPriceComponentType Type, string Code, string Description, string Unit,
    decimal Quantity, decimal UnitPrice, decimal LineAmount);
public sealed record CustomerShipmentInvoiceLineResponse(Guid Id, string Description,
    decimal Quantity, string Unit, decimal UnitPrice, decimal TaxRate, decimal LineTotal);
public sealed record CustomerShipmentResponse(Guid Id, string InvoiceNumber, long RowVersion,
    Guid BranchId, string Branch, string Currency, Guid ClientId, string ClientNumber,
    string ClientName, string ClientPhone, string? ClientEmail, string ReceiverName,
    CustomerShipmentMethod Method, string OriginCountryCode, string OriginCity,
    string DestinationCountryCode, string DestinationCityOrState, string? DeliveryAddress,
    ShipmentHandoverMethod HandoverMethod, ShipmentGoodsType GoodsType, string Description,
    string? Contents, int PieceCount, CustomerShipmentPricingMethod PricingMethod,
    string? ServiceCode, decimal? ActualWeightKg, decimal? ChargeableWeightKg,
    decimal? LengthCm, decimal? WidthCm, decimal? HeightCm, string? NegotiatedReason,
    CustomerShipmentMovementState MovementState, ShipmentPaymentState PaymentState,
    decimal Subtotal, decimal Tax, decimal Total, decimal Paid, decimal CreditApplied,
    decimal Balance, PaymentMethod? PaymentMethod, Guid? ReferredByClientId,
    string? CustomerNote, DateTime CreatedAt, DateTime? LastModifiedAt,
    IReadOnlyList<CustomerShipmentPriceComponentResponse> Pricing,
    IReadOnlyList<CustomerShipmentInvoiceLineResponse> InvoiceLines);
