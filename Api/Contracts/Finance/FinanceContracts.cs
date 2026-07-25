using Domain.Enums;

namespace Api.Controllers;

public record CreateInvoiceRequest(Guid CustomerId, DateTime IssueDate, DateTime DueDate, string Currency,
    decimal DiscountAmount, string? Notes, IReadOnlyList<CreateInvoiceLineRequest> Lines,
    InvoiceType Type = InvoiceType.Sales, Guid? OriginalInvoiceId = null, string? IssuerBranchCode = null,
    PaymentMethod? PreferredPaymentMethod = null);
public record CreateInvoiceLineRequest(string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
public record InvoiceLineResponse(Guid Id, string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate, decimal LineTotal);
public record InvoiceResponse(Guid Id, string InvoiceNumber, Guid CustomerId, string CustomerName,
    InvoiceType Type, Guid? OriginalInvoiceId, string IssuerBranchCode, DateTime IssueDate, DateTime DueDate,
    InvoiceStatus Status, InvoicePaymentStatus PaymentStatus, PaymentMethod? PreferredPaymentMethod,
    string Currency, decimal Subtotal,
    decimal TaxAmount, decimal DiscountAmount, decimal TotalAmount, decimal AmountPaid,
    decimal AppliedCreditAmount, decimal BalanceDue, string? Notes, string VerificationCode,
    DateTime? LastModifiedAt,
    IReadOnlyList<InvoiceLineResponse> Lines);
public record RecordPaymentRequest(Guid InvoiceId, string Reference, decimal Amount, string Currency,
    PaymentMethod Method, DateTime PaidAt, string? Notes, Guid? BankAccountId = null);
public record CreateExpenseRequest(string Category, string Description, string Payee, decimal Amount,
    string Currency, DateTime IncurredAt, string BranchCode);
public record ExpenseDecisionRequest(ExpenseStatus Status);
public record ConvertCurrencyRequest(decimal Amount, string SourceCurrency, string TargetCurrency);
public record UpdateInvoiceByNumberRequest(string? Notes, PaymentMethod? PreferredPaymentMethod);
public record RecordInvoicePaymentByNumberRequest(string Reference, decimal Amount,
    PaymentMethod Method, DateTime PaidAt, string? Notes, Guid? BankAccountId = null);
public record VoidReceiptRequest(string Reason);
