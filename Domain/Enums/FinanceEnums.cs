namespace Domain.Enums;

public enum InvoiceStatus { Draft = 0, Issued = 1, PartiallyPaid = 2, Paid = 3, Overdue = 4, Void = 5 }
public enum InvoiceType { Sales = 0, CreditNote = 1 }
public enum InvoicePaymentStatus { Unpaid = 0, PartiallyPaid = 1, Paid = 2, NotApplicable = 3 }

public enum CreditNoteState
{
    CreditAvailable = 0,
    CreditPartUsed = 1,
    CreditUsed = 2,
    Voided = 3
}
public enum PaymentStatus { Pending = 0, Confirmed = 1, Failed = 2, Reversed = 3 }
public enum PaymentMethod { Cash = 0, BankTransfer = 1, Card = 2, Wallet = 3, Other = 4 }
public enum ExpenseStatus { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3, Paid = 4, Void = 5 }
public enum SupplierBillStatus { Draft = 0, Submitted = 1, Approved = 2, PartiallyPaid = 3, Paid = 4, Void = 5 }
public enum PurchaseOrderStatus { Draft = 0, Approved = 1, Sent = 2, PartiallyReceived = 3, Received = 4, Cancelled = 5 }
