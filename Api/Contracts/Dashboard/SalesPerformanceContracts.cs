namespace Api.Controllers;

public sealed record SalesPerformanceResponse(DateTime From, DateTime To, string Currency,
    SalesPerformanceTotals Totals, IReadOnlyList<SalesPerformancePeriod> Periods);

public sealed record SalesPerformanceTotals(int Quotations, int AcceptedQuotations,
    int SalesInvoices, int CreditNotes, int Receipts, decimal QuotedValue,
    decimal InvoicedValue, decimal CollectedValue, decimal OutstandingValue);

public sealed record SalesPerformancePeriod(DateTime PeriodStart, DateTime PeriodEnd,
    int Quotations, int AcceptedQuotations, int SalesInvoices, int CreditNotes, int Receipts,
    decimal QuotedValue, decimal InvoicedValue, decimal CollectedValue, decimal OutstandingValue);
