using Domain.Enums;

namespace Api.Controllers;

public sealed record LoyaltyTransactionResponse(Guid Id, LoyaltyPointTransactionType Type, int Points,
    Guid? ReferredCustomerId, string? ReferredCustomerName, Guid? InvoiceId, string? InvoiceNumber,
    string Description, DateTime OccurredAt);

public sealed record LoyaltyAccountResponse(Guid CustomerId, string CustomerName, int AvailablePoints,
    int LifetimeEarnedPoints);
