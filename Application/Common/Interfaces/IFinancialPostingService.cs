using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces;

public sealed record InvoicePaymentCommand(Guid InvoiceId, string Reference, decimal Amount, string Currency,
    PaymentMethod Method, Guid? BankAccountId, DateTime PaidAt, string? Notes, Guid RecordedByUserId);

public sealed record PurchasePayoutCommand(Guid CompanyPurchaseId, string Reference, decimal Amount,
    string Currency, PaymentMethod Method, Guid? BankAccountId, DateTime PaidAt, string? Notes,
    Guid RecordedByUserId);

public sealed record FinancialPostingResult(Payment? Payment, PurchasePayout? Payout,
    FinancialTransaction Transaction, TransactionDocument Document);

public sealed record ReceiptVoidCommand(string ReceiptNumber, string Reason, Guid VoidedByUserId);
public sealed record ReceiptVoidResult(TransactionDocument Receipt,
    FinancialTransaction ReversalTransaction, Invoice Invoice);

public interface IFinancialPostingService
{
    Task<FinancialPostingResult> RecordInvoicePaymentAsync(InvoicePaymentCommand command,
        CancellationToken cancellationToken);
    Task<FinancialPostingResult> RecordPurchasePayoutAsync(PurchasePayoutCommand command,
        CancellationToken cancellationToken);
    Task<ReceiptVoidResult> VoidReceiptAsync(ReceiptVoidCommand command,
        CancellationToken cancellationToken);
}
