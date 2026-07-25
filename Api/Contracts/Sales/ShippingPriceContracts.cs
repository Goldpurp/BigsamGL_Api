using Domain.Enums;

namespace Api.Controllers.Sales;

public sealed record SaveShippingPriceRequest(string Code, string Category, string Group,
    string Name, string Description, string ServiceArea, ShippingPriceServiceMethod Method,
    string Unit, ShippingPriceKind PriceKind, decimal? LowerPrice, decimal? UpperPrice,
    decimal? HandlingFee, decimal? MinimumCharge, string? DispatchSchedule,
    string? EstimatedDelivery, string Reason, string? Currency = null,
    string? ServiceCode = null, string? DestinationCountryCodes = null,
    string? DestinationZone = null, decimal? DocumentationFee = null,
    decimal? MinimumQuantity = null, decimal? MaximumQuantity = null,
    bool IsPublic = true, int DisplayOrder = 0, Guid? ServiceRouteId = null);
public sealed record ShippingPriceResponse(Guid Id, long RowVersion, Guid BranchId, string Branch,
    string Currency, string ServiceCode, string Code, string Category, string Group, string Name,
    string Description, string ServiceArea, string DestinationCountryCodes, string DestinationZone,
    ShippingPriceServiceMethod Method, string Unit, ShippingPriceKind PriceKind,
    decimal? LowerPrice, decimal? UpperPrice, decimal? HandlingFee, decimal? DocumentationFee,
    decimal? MinimumCharge, decimal? MinimumQuantity, decimal? MaximumQuantity,
    string? DispatchSchedule, string? EstimatedDelivery, ShippingPriceSource Source,
    ShippingPriceState State, bool IsPublic, int DisplayOrder,
    DateTime CreatedAt, DateTime? LastModifiedAt, Guid? ServiceRouteId);
public sealed record ShippingPriceStateRequest(string Reason);
