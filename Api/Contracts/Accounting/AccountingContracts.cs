using Domain.Enums;

namespace Api.Controllers;

public record CreateJournalRequest(DateTime EntryDate, string Description, string Currency, IReadOnlyList<CreateJournalLineRequest> Lines);
public record CreateJournalLineRequest(string AccountCode, decimal Debit, decimal Credit, string Description);
public record CreateSupplierRequest(string Name, string Email, string Phone, string CountryCode, string TaxNumber);
public record CreateSupplierBillRequest(Guid SupplierId, string SupplierReference, DateTime BillDate, DateTime DueDate,
    decimal NetAmount, decimal TaxAmount, string Currency, string Description);
public record CreatePurchaseOrderRequest(Guid SupplierId, DateTime OrderDate, DateTime? ExpectedAt, decimal TotalAmount, string Currency, string Description);
public record ChangePurchaseOrderStatusRequest(PurchaseOrderStatus Status);
public record CreateBankAccountRequest(string Name, string BankName, string MaskedAccountNumber, string Currency, decimal OpeningBalance);
public record AccountingPeriodRequest(string Currency, string Reason);
public record ReverseJournalRequest(string Reason);
