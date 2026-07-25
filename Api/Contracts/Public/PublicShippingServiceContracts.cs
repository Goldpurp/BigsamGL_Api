using Domain.Enums;

namespace Api.Controllers.Public;

public sealed record PublicShippingPriceResponse(Guid Id, string Code, string Name,
    string Category, string Group, string Description, string Unit,
    ShippingPriceKind PriceKind, decimal? Price, decimal? UpperPrice,
    decimal? MinimumQuantity, decimal? MaximumQuantity,
    decimal? HandlingFee, decimal? DocumentationFee, decimal? MinimumCharge,
    int DisplayOrder);

public sealed record PublicShippingServiceResponse(string ServiceCode, Guid OriginHubId,
    string OriginHubCode, string OriginHubName, string OriginCountryCode,
    string ServiceArea, string DestinationZone, IReadOnlyList<string> DestinationCountryCodes,
    ShippingPriceServiceMethod Method, string Currency, string? DispatchSchedule,
    string? EstimatedDelivery, IReadOnlyList<PublicShippingPriceResponse> Prices);

public sealed record PublicShippingPriceSelection(string Code, decimal Quantity);

public sealed record PublicShippingEstimateRequest(string ServiceCode,
    decimal ActualWeightKg, decimal? LengthCm, decimal? WidthCm, decimal? HeightCm,
    IReadOnlyList<PublicShippingPriceSelection>? Selections);

public sealed record PublicShippingEstimateLine(string Code, string Description,
    decimal Quantity, string Unit, decimal UnitPrice, decimal Amount);

public sealed record PublicShippingEstimateResponse(string ServiceCode, string Currency,
    decimal ActualWeightKg, decimal? VolumetricWeightKg, decimal? VolumeCbm,
    decimal BillableQuantity, string BillableUnit, decimal HandlingFee,
    decimal DocumentationFee, decimal EstimatedTotal,
    IReadOnlyList<PublicShippingEstimateLine> Lines,
    string Notice);
