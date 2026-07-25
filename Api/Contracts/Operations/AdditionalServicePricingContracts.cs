using Domain.Enums;

namespace Api.Controllers.Operations;

public sealed record CreateAdditionalServicePriceRequest(string Code, string Name, string Description,
    AdditionalServiceCategory Category, PriceUnit Unit, decimal Amount, string Currency,
    string BranchCode, DateTime EffectiveFrom, DateTime? EffectiveTo);

public sealed record ReplaceAdditionalServicePriceRequest(string Name, string Description,
    AdditionalServiceCategory Category, PriceUnit Unit, decimal Amount, string Currency,
    DateTime EffectiveFrom);

