using Domain.Enums;

namespace Api.Controllers;

public sealed record FinancialTransactionListItem(Guid Id, string TransactionNumber,
    FinancialTransactionDirection Direction, FinancialTransactionType Type, FinancialTransactionStatus Status,
    string SourceType, Guid SourceId, string CounterpartyName, decimal Amount, string Currency,
    PaymentMethod Method, string SettlementAccountCode, Guid? BankAccountId, string ExternalReference,
    DateTime OccurredAt, string BranchCode, FreightClassification FreightClassification,
    Guid? JournalEntryId);

public sealed record TransactionDocumentListItem(Guid Id, string DocumentNumber, FinancialDocumentType Type,
    FinancialDocumentStatus Status, Guid FinancialTransactionId, string TransactionNumber,
    Guid? InvoiceId, Guid? PaymentId, Guid? CompanyPurchaseId, Guid? PurchasePayoutId,
    string CounterpartyName, decimal Amount, string Currency, PaymentMethod Method,
    DateTime IssuedAt, string VerificationCode);

public sealed record RecordFinancialInflowRequest(string Counterparty, decimal Amount, string Currency,
    PaymentMethod Method, string Reference, string Description, FinancialTransactionType Type,
    DateTime OccurredAt, FreightClassification FreightClassification);
