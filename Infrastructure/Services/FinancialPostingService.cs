using System.Data;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class FinancialPostingService : IFinancialPostingService
{
    private readonly ApplicationDbContext _context;
    private readonly IAccountingService _accounting;
    private readonly ICompanyDocumentNumberGenerator _numbers;

    public FinancialPostingService(ApplicationDbContext context, IAccountingService accounting,
        ICompanyDocumentNumberGenerator numbers)
    {
        _context = context;
        _accounting = accounting;
        _numbers = numbers;
    }

    public async Task<FinancialPostingResult> RecordInvoicePaymentAsync(InvoicePaymentCommand command,
        CancellationToken cancellationToken)
    {
        ValidatePayment(command.Amount, command.Currency, command.Method);
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM invoices WHERE \"Id\" = {command.InvoiceId} FOR UPDATE", cancellationToken);
            var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == command.InvoiceId,
                cancellationToken) ?? throw new KeyNotFoundException("Invoice not found.");
            if (invoice.Status is InvoiceStatus.Void or InvoiceStatus.Draft)
                throw new InvalidOperationException("Payment cannot be applied to this invoice.");
            if (invoice.Type == InvoiceType.CreditNote)
                throw new InvalidOperationException("Payments cannot be applied to a credit note.");
            if (!string.Equals(invoice.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Payment and invoice currencies must match.");
            if (command.Amount > invoice.BalanceDue)
                throw new ArgumentException("Payment exceeds the outstanding invoice balance.");
            var reference = command.Reference.Trim();
            if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Payment reference is required.");
            if (await _context.Payments.AnyAsync(x => x.Reference == reference, cancellationToken))
                throw new InvalidOperationException("The payment reference has already been recorded.");
            var settlementAccount = await ValidateSettlementAccountAsync(command.Method, command.BankAccountId,
                invoice.Currency, cancellationToken);

            var payment = new Payment
            {
                BranchId = invoice.BranchId,
                InvoiceId = invoice.Id,
                CustomerId = invoice.CustomerId,
                Reference = reference,
                Amount = command.Amount,
                Currency = invoice.Currency,
                Method = command.Method,
                BankAccountId = command.BankAccountId,
                SettlementAccountCode = settlementAccount,
                PaidAt = command.PaidAt,
                Notes = command.Notes?.Trim(),
                Status = PaymentStatus.Confirmed
            };
            var financial = new FinancialTransaction
            {
                TransactionNumber = await _numbers.NextTransactionAsync(cancellationToken),
                BranchId = invoice.BranchId,
                Direction = FinancialTransactionDirection.Credit,
                Type = FinancialTransactionType.SalesPayment,
                SourceType = nameof(Payment),
                SourceId = payment.Id,
                CounterpartyId = invoice.CustomerId,
                CounterpartyName = invoice.CustomerName,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Method = payment.Method,
                SettlementAccountCode = settlementAccount,
                BankAccountId = payment.BankAccountId,
                ExternalReference = payment.Reference,
                OccurredAt = payment.PaidAt,
                BranchCode = invoice.IssuerBranchCode,
                RecordedByUserId = command.RecordedByUserId
            };
            var receipt = new TransactionDocument
            {
                DocumentNumber = await _numbers.NextFinancialDocumentAsync(FinancialDocumentType.Receipt,
                    cancellationToken),
                Type = FinancialDocumentType.Receipt,
                FinancialTransactionId = financial.Id,
                InvoiceId = invoice.Id,
                PaymentId = payment.Id,
                IssuedAt = DateTime.UtcNow,
                IssuedByUserId = command.RecordedByUserId,
                VerificationCode = DocumentSecurity.NewVerificationCode()
            };
            invoice.AmountPaid += payment.Amount;
            SyncPaymentState(invoice);
            _context.Payments.Add(payment);
            _context.FinancialTransactions.Add(financial);
            _context.TransactionDocuments.Add(receipt);
            var journal = await _accounting.PrepareJournalAsync("Payment", payment.Id,
                $"Payment {payment.Reference}", payment.PaidAt, payment.Currency,
                new[]
                {
                    new JournalPostingLine(settlementAccount, payment.Amount, 0, $"Payment {payment.Reference}"),
                    new JournalPostingLine("1100", 0, payment.Amount, $"Settlement of {invoice.InvoiceNumber}")
                }, cancellationToken, invoice.BranchId);
            financial.JournalEntryId = journal.Id;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new FinancialPostingResult(payment, null, financial, receipt);
        });
    }

    public async Task<FinancialPostingResult> RecordPurchasePayoutAsync(PurchasePayoutCommand command,
        CancellationToken cancellationToken)
    {
        ValidatePayment(command.Amount, command.Currency, command.Method);
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM company_purchases WHERE \"Id\" = {command.CompanyPurchaseId} FOR UPDATE",
                cancellationToken);
            var purchase = await _context.CompanyPurchases.FirstOrDefaultAsync(
                x => x.Id == command.CompanyPurchaseId, cancellationToken)
                ?? throw new KeyNotFoundException("Company purchase not found.");
            if (purchase.Status is not (CompanyPurchaseStatus.Approved or CompanyPurchaseStatus.PartiallyPaid))
                throw new InvalidOperationException("Only approved purchases can be paid.");
            if (!string.Equals(purchase.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Payout and purchase currencies must match.");
            if (command.Amount > purchase.BalanceDue)
                throw new ArgumentException("Payout exceeds the outstanding purchase balance.");
            var reference = command.Reference.Trim();
            if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Payout reference is required.");
            if (await _context.PurchasePayouts.AnyAsync(x => x.Reference == reference, cancellationToken))
                throw new InvalidOperationException("The payout reference has already been recorded.");
            var settlementAccount = await ValidateSettlementAccountAsync(command.Method, command.BankAccountId,
                purchase.Currency, cancellationToken);
            var branchId = await _context.Branches.AsNoTracking()
                .Where(x => x.Code == purchase.BranchCode && x.IsActive)
                .Select(x => x.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (branchId == Guid.Empty)
                throw new InvalidOperationException("The purchase branch was not found or is inactive.");
            var payout = new PurchasePayout
            {
                PayoutNumber = await _numbers.NextPayoutAsync(cancellationToken),
                CompanyPurchaseId = purchase.Id,
                Reference = reference,
                Amount = command.Amount,
                Currency = purchase.Currency,
                Method = command.Method,
                BankAccountId = command.BankAccountId,
                SettlementAccountCode = settlementAccount,
                PaidAt = command.PaidAt,
                PaidByUserId = command.RecordedByUserId,
                Notes = command.Notes?.Trim()
            };
            var financial = new FinancialTransaction
            {
                TransactionNumber = await _numbers.NextTransactionAsync(cancellationToken),
                BranchId = branchId,
                Direction = FinancialTransactionDirection.Debit,
                Type = FinancialTransactionType.Purchase,
                SourceType = nameof(PurchasePayout),
                SourceId = payout.Id,
                CounterpartyId = purchase.SupplierId,
                CounterpartyName = purchase.VendorName,
                Amount = payout.Amount,
                Currency = payout.Currency,
                Method = payout.Method,
                SettlementAccountCode = settlementAccount,
                BankAccountId = payout.BankAccountId,
                ExternalReference = payout.Reference,
                OccurredAt = payout.PaidAt,
                BranchCode = purchase.BranchCode,
                RecordedByUserId = command.RecordedByUserId
            };
            var voucher = new TransactionDocument
            {
                DocumentNumber = await _numbers.NextFinancialDocumentAsync(FinancialDocumentType.PaymentVoucher,
                    cancellationToken),
                Type = FinancialDocumentType.PaymentVoucher,
                FinancialTransactionId = financial.Id,
                CompanyPurchaseId = purchase.Id,
                PurchasePayoutId = payout.Id,
                IssuedAt = DateTime.UtcNow,
                IssuedByUserId = command.RecordedByUserId,
                VerificationCode = DocumentSecurity.NewVerificationCode()
            };
            payout.FinancialTransactionId = financial.Id;
            payout.TransactionDocumentId = voucher.Id;
            purchase.AmountPaid += payout.Amount;
            purchase.Status = purchase.BalanceDue == 0
                ? CompanyPurchaseStatus.Paid : CompanyPurchaseStatus.PartiallyPaid;
            _context.FinancialTransactions.Add(financial);
            _context.TransactionDocuments.Add(voucher);
            _context.PurchasePayouts.Add(payout);
            var journal = await _accounting.PrepareJournalAsync("PurchasePayout", payout.Id,
                $"Payout {payout.PayoutNumber} for {purchase.PurchaseNumber}", payout.PaidAt, payout.Currency,
                new[]
                {
                    new JournalPostingLine("2000", payout.Amount, 0, $"Settlement of {purchase.PurchaseNumber}"),
                    new JournalPostingLine(settlementAccount, 0, payout.Amount, $"Payout {payout.Reference}")
                }, cancellationToken, branchId);
            financial.JournalEntryId = journal.Id;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new FinancialPostingResult(null, payout, financial, voucher);
        });
    }

    public async Task<ReceiptVoidResult> VoidReceiptAsync(ReceiptVoidCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("A receipt void reason is required.");
        var receiptNumber = command.ReceiptNumber.Trim().ToUpperInvariant();
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM transaction_documents WHERE \"DocumentNumber\" = {receiptNumber} FOR UPDATE",
                cancellationToken);
            var receipt = await _context.TransactionDocuments.FirstOrDefaultAsync(x =>
                x.DocumentNumber == receiptNumber && x.Type == FinancialDocumentType.Receipt,
                cancellationToken) ?? throw new KeyNotFoundException("Receipt not found.");
            if (receipt.Status == FinancialDocumentStatus.Voided)
                throw new InvalidOperationException("This receipt has already been voided.");
            if (!receipt.PaymentId.HasValue || !receipt.InvoiceId.HasValue)
                throw new InvalidOperationException("The receipt is not linked to an invoice payment.");
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM invoices WHERE \"Id\" = {receipt.InvoiceId.Value} FOR UPDATE",
                cancellationToken);
            var payment = await _context.Payments.FirstAsync(x => x.Id == receipt.PaymentId.Value,
                cancellationToken);
            var invoice = await _context.Invoices.FirstAsync(x => x.Id == receipt.InvoiceId.Value,
                cancellationToken);
            var original = await _context.FinancialTransactions.FirstAsync(x =>
                x.Id == receipt.FinancialTransactionId, cancellationToken);

            receipt.Status = FinancialDocumentStatus.Voided;
            receipt.VoidedAt = DateTime.UtcNow;
            receipt.VoidedByUserId = command.VoidedByUserId;
            receipt.VoidReason = command.Reason.Trim();
            payment.Status = PaymentStatus.Reversed;
            original.Status = FinancialTransactionStatus.Reversed;
            invoice.AmountPaid = Math.Max(0, invoice.AmountPaid - payment.Amount);
            if (invoice.BalanceDue == 0)
            {
                invoice.PaymentStatus = InvoicePaymentStatus.Paid;
                invoice.Status = InvoiceStatus.Paid;
            }
            else if (invoice.AmountPaid > 0 || invoice.AppliedCreditAmount > 0)
            {
                invoice.PaymentStatus = InvoicePaymentStatus.PartiallyPaid;
                invoice.Status = InvoiceStatus.PartiallyPaid;
            }
            else
            {
                invoice.PaymentStatus = InvoicePaymentStatus.Unpaid;
                invoice.Status = InvoiceStatus.Issued;
            }

            var reversal = new FinancialTransaction
            {
                TransactionNumber = await _numbers.NextTransactionAsync(cancellationToken),
                BranchId = original.BranchId,
                Direction = original.Direction == FinancialTransactionDirection.Credit
                    ? FinancialTransactionDirection.Debit : FinancialTransactionDirection.Credit,
                Type = FinancialTransactionType.Reversal,
                SourceType = "ReceiptVoid",
                SourceId = receipt.Id,
                CounterpartyId = original.CounterpartyId,
                CounterpartyName = original.CounterpartyName,
                Amount = original.Amount,
                Currency = original.Currency,
                Method = original.Method,
                SettlementAccountCode = original.SettlementAccountCode,
                BankAccountId = original.BankAccountId,
                ExternalReference = $"VOID-{receipt.DocumentNumber}",
                OccurredAt = DateTime.UtcNow,
                BranchCode = original.BranchCode,
                RecordedByUserId = command.VoidedByUserId,
                FreightClassification = original.FreightClassification,
                FreightClassifiedAt = DateTime.UtcNow,
                ReversalOfTransactionId = original.Id
            };
            _context.FinancialTransactions.Add(reversal);

            IReadOnlyList<JournalPostingLine> reversalLines;
            if (original.JournalEntryId.HasValue)
            {
                reversalLines = await (from line in _context.JournalLines.AsNoTracking()
                    join account in _context.LedgerAccounts.AsNoTracking()
                        on line.LedgerAccountId equals account.Id
                    where line.JournalEntryId == original.JournalEntryId
                    select new JournalPostingLine(account.Code, line.Credit, line.Debit,
                        $"Reverse {receipt.DocumentNumber}: {line.Description}"))
                    .ToListAsync(cancellationToken);
            }
            else
            {
                reversalLines =
                [
                    new JournalPostingLine("1100", payment.Amount, 0,
                        $"Restore receivable for {invoice.InvoiceNumber}"),
                    new JournalPostingLine(payment.SettlementAccountCode, 0, payment.Amount,
                        $"Reverse receipt {receipt.DocumentNumber}")
                ];
            }
            var journal = await _accounting.PrepareJournalAsync("ReceiptVoid", receipt.Id,
                $"Void receipt {receipt.DocumentNumber}: {receipt.VoidReason}", DateTime.UtcNow,
                payment.Currency, reversalLines, cancellationToken, original.BranchId);
            journal.ReversalOfJournalId = original.JournalEntryId;
            reversal.JournalEntryId = journal.Id;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ReceiptVoidResult(receipt, reversal, invoice);
        });
    }

    private async Task<string> ValidateSettlementAccountAsync(PaymentMethod method, Guid? bankAccountId,
        string currency, CancellationToken cancellationToken)
    {
        var accountCode = PaymentRouting.AccountCode(method);
        if (method == PaymentMethod.Cash && bankAccountId.HasValue)
            throw new ArgumentException("Cash transactions cannot be assigned to a bank account.");
        if (!bankAccountId.HasValue) return accountCode;
        var account = await _context.BankAccounts.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == bankAccountId && x.IsActive, cancellationToken)
            ?? throw new ArgumentException("Settlement bank account was not found or is inactive.");
        if (!string.Equals(account.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Settlement bank account currency does not match the transaction.");
        return accountCode;
    }

    private static void ValidatePayment(decimal amount, string currency, PaymentMethod method)
    {
        if (amount <= 0) throw new ArgumentException("Transaction amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new ArgumentException("A three-letter transaction currency is required.");
        if (!PaymentRouting.IsSupported(method))
            throw new ArgumentException("Payment method must be Cash, BankTransfer, or Card.");
    }

    private static void SyncPaymentState(Invoice invoice)
    {
        if (invoice.BalanceDue == 0)
        {
            invoice.PaymentStatus = InvoicePaymentStatus.Paid;
            invoice.Status = InvoiceStatus.Paid;
        }
        else
        {
            invoice.PaymentStatus = InvoicePaymentStatus.PartiallyPaid;
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }
    }
}
