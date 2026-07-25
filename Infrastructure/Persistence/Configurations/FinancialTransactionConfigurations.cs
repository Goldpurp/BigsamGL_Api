using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("financial_transactions", table =>
        {
            table.HasCheckConstraint("CK_financial_transactions_amount", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_financial_transactions_currency", "char_length(\"Currency\") = 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BranchId).IsRequired();
        builder.HasIndex(x => x.TransactionNumber).IsUnique();
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceType).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.SourceType, x.SourceId }).IsUnique();
        builder.Property(x => x.CounterpartyName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.SettlementAccountCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ExternalReference).HasMaxLength(120).IsRequired();
        builder.Property(x => x.BranchCode).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.Direction, x.Status, x.OccurredAt });
        builder.HasIndex(x => new { x.Type, x.OccurredAt });
        builder.HasIndex(x => new { x.Currency, x.OccurredAt });
        builder.HasIndex(x => new { x.BranchId, x.Currency, x.OccurredAt });
        builder.HasIndex(x => new { x.BranchId, x.Currency, x.Direction, x.Type, x.OccurredAt });
        builder.Property(x => x.FreightClassification).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.FreightClassification, x.OccurredAt });
        builder.HasIndex(x => x.CounterpartyId);
        builder.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialTransaction>().WithMany().HasForeignKey(x => x.ReversalOfTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransactionDocumentConfiguration : IEntityTypeConfiguration<TransactionDocument>
{
    public void Configure(EntityTypeBuilder<TransactionDocument> builder)
    {
        builder.ToTable("transaction_documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.VerificationCode).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.VerificationCode).IsUnique();
        builder.Property(x => x.VoidReason).HasMaxLength(1000);
        builder.HasIndex(x => x.FinancialTransactionId).IsUnique();
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => x.PaymentId).IsUnique().HasFilter("\"PaymentId\" IS NOT NULL");
        builder.HasIndex(x => x.CompanyPurchaseId);
        builder.HasIndex(x => x.PurchasePayoutId).IsUnique().HasFilter("\"PurchasePayoutId\" IS NOT NULL");
        builder.HasIndex(x => new { x.Type, x.Status, x.IssuedAt });
        builder.HasOne<FinancialTransaction>().WithOne().HasForeignKey<TransactionDocument>(x => x.FinancialTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithOne().HasForeignKey<TransactionDocument>(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyPurchase>().WithMany().HasForeignKey(x => x.CompanyPurchaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

