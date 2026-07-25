using Domain.Enums;

namespace Api.Controllers;

public sealed record CreateCreditNoteRequest(Guid ClientId, string? OriginalInvoiceNumber,
    decimal Amount, string Reason);
public sealed record ApplyCreditSelection(Guid CreditNoteId, decimal Amount);
public sealed record ApplyInvoiceCreditRequest(IReadOnlyList<ApplyCreditSelection> Credits);
public sealed record VoidCreditNoteRequest(string Reason);
public sealed record CreditApplicationResponse(Guid Id, Guid CreditNoteId, string CreditNoteNumber,
    decimal Amount, DateTime AppliedAt);
public sealed record CreditNoteResponse(Guid Id, string CreditNoteNumber, long RowVersion,
    Guid BranchId, Guid ClientId, string ClientNumber, string ClientName, Guid? OriginalInvoiceId,
    string? OriginalInvoiceNumber, string Currency, decimal OriginalAmount, decimal UsedAmount,
    decimal AvailableAmount, CreditNoteState State, string Reason, Guid ApprovedByUserId,
    DateTime IssuedAt, DateTime? VoidedAt, string? VoidReason);
public sealed record AvailableClientCreditResponse(Guid ClientId, string Currency,
    decimal AvailableAmount, IReadOnlyList<CreditNoteResponse> Credits);
public sealed record InvoiceCreditApplicationResponse(string InvoiceNumber, decimal AppliedNow,
    decimal TotalCreditApplied, decimal AmountPaid, decimal Balance, InvoicePaymentStatus PaymentStatus,
    IReadOnlyList<CreditApplicationResponse> Applications);
