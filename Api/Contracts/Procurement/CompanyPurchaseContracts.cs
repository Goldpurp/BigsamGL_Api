using Domain.Enums;

namespace Api.Controllers;

public sealed record CompanyPurchaseLineRequest(string Description, decimal Quantity,
    decimal UnitPrice, decimal TaxRate, Guid? InventoryItemId = null);

public sealed record CreateCompanyPurchaseRequest(Guid? SupplierId, string VendorName,
    CompanyPurchaseType? Type, CompanyPurchaseCategory Category, string BranchCode, DateTime PurchaseDate, DateTime? DueDate,
    string Currency, string Description, string SupplierReference,
    IReadOnlyList<CompanyPurchaseLineRequest> Lines);

public sealed record DecideCompanyPurchaseRequest(CompanyPurchaseStatus Status, string Reason);

public sealed record RecordPurchasePayoutRequest(string Reference, decimal Amount, string Currency,
    PaymentMethod Method, Guid? BankAccountId, DateTime PaidAt, string? Notes);

public sealed record ReceivePurchaseLineRequest(Guid CompanyPurchaseLineId,
    Guid InventoryLocationId, decimal Quantity);

public sealed record ReceiveCompanyPurchaseRequest(string IdempotencyKey, DateTime OccurredAt,
    string? Notes, IReadOnlyList<ReceivePurchaseLineRequest> Lines);
