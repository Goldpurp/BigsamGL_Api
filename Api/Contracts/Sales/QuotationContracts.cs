using Domain.Enums;

namespace Api.Controllers;

public sealed record SalesQuotationLineRequest(string Description, decimal Quantity,
    decimal UnitPrice);

public sealed record CreateSalesQuotationRequest(Guid CustomerId, string Route,
    DateTime ValidUntil, string? Note, QuoteStatus InitialStatus,
    IReadOnlyList<SalesQuotationLineRequest> Lines);

public sealed record ChangeSalesQuotationStatusRequest(QuoteStatus Status, string? Reason);
