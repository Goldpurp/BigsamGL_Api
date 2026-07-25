namespace Domain.Enums;

public enum FinancialTransactionDirection { Credit = 0, Debit = 1 }
public enum FinancialTransactionType
{
    SalesPayment = 0,
    Purchase = 1,
    Expense = 2,
    OtherIncome = 3,
    CapitalIntroduced = 4,
    LoanReceived = 5,
    LoanPayment = 6,
    Adjustment = 7,
    Reversal = 8,
    OtherOutflow = 9
}
public enum FinancialTransactionStatus { Posted = 0, Reversed = 1 }
public enum FinancialDocumentType { Receipt = 0, PaymentVoucher = 1 }
public enum FinancialDocumentStatus { Issued = 0, Voided = 1 }
