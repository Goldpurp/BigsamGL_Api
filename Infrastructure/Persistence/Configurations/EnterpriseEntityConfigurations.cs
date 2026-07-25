using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class AccountActionTokenConfiguration : IEntityTypeConfiguration<AccountActionToken>
{
    public void Configure(EntityTypeBuilder<AccountActionToken> b)
    {
        b.ToTable("account_action_tokens"); b.HasKey(x => x.Id); b.Property(x => x.Purpose).HasMaxLength(40).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired(); b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserAccountId, x.Purpose, x.ExpiresAt });
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesActivityConfiguration : IEntityTypeConfiguration<SalesActivity>
{
    public void Configure(EntityTypeBuilder<SalesActivity> b)
    {
        b.ToTable("sales_activities"); b.HasKey(x => x.Id); b.Property(x => x.ActivityType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(500).IsRequired(); b.HasIndex(x => new { x.SalesLeadId, x.OccurredAt });
        b.HasOne<SalesLead>().WithMany().HasForeignKey(x => x.SalesLeadId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalesTargetConfiguration : IEntityTypeConfiguration<SalesTarget>
{
    public void Configure(EntityTypeBuilder<SalesTarget> b)
    {
        b.ToTable("sales_targets"); b.HasKey(x => x.Id); b.Property(x => x.RevenueTarget).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired(); b.HasIndex(x => new { x.StaffUserId, x.PeriodStart, x.PeriodEnd }).IsUnique();
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> b)
    {
        b.ToTable("ledger_accounts"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(20).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired(); b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20); b.Property(x => x.Currency).HasMaxLength(3);
    }
}

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> b)
    {
        b.ToTable("journal_entries"); b.HasKey(x => x.Id); b.Property(x => x.JournalNumber).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.JournalNumber).IsUnique();
        b.Property(x => x.Description).HasMaxLength(500).IsRequired(); b.Property(x => x.SourceType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); b.HasIndex(x => new { x.EntryDate, x.Status });
        b.HasIndex(x => new { x.BranchId, x.Currency, x.EntryDate });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.EntryDate });
        b.HasIndex(x => new { x.SourceType, x.SourceId }).IsUnique().HasFilter("\"SourceId\" IS NOT NULL");
        b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReversalOfJournalId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("accounting_periods");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Period).HasMaxLength(7).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.BranchId, x.Currency, x.Period }).IsUnique();
        builder.HasIndex(x => new { x.IsClosed, x.Period });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> b)
    {
        b.ToTable("journal_lines"); b.HasKey(x => x.Id); b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Debit).HasPrecision(18, 2); b.Property(x => x.Credit).HasPrecision(18, 2); b.Property(x => x.ExchangeRate).HasPrecision(18, 6);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired(); b.HasIndex(x => new { x.JournalEntryId, x.LedgerAccountId });
        b.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<LedgerAccount>().WithMany().HasForeignKey(x => x.LedgerAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("suppliers"); b.HasKey(x => x.Id); b.Property(x => x.SupplierNumber).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.SupplierNumber).IsUnique();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired(); b.Property(x => x.Email).HasMaxLength(254); b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.CountryCode).HasMaxLength(5); b.Property(x => x.TaxNumber).HasMaxLength(80);
        b.HasIndex(x => x.Name);
    }
}

public class SupplierBillConfiguration : IEntityTypeConfiguration<SupplierBill>
{
    public void Configure(EntityTypeBuilder<SupplierBill> b)
    {
        b.ToTable("supplier_bills"); b.HasKey(x => x.Id); b.Property(x => x.BillNumber).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.BillNumber).IsUnique();
        b.Property(x => x.SupplierReference).HasMaxLength(100); b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30); b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.NetAmount).HasPrecision(18, 2); b.Property(x => x.TaxAmount).HasPrecision(18, 2); b.Property(x => x.TotalAmount).HasPrecision(18, 2); b.Property(x => x.AmountPaid).HasPrecision(18, 2);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable("purchase_orders"); b.HasKey(x => x.Id); b.Property(x => x.PurchaseOrderNumber).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.PurchaseOrderNumber).IsUnique();
        b.Property(x => x.TotalAmount).HasPrecision(18, 2); b.Property(x => x.Currency).HasMaxLength(3); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Description).HasMaxLength(500).IsRequired(); b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> b)
    {
        b.ToTable("bank_accounts"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(120).IsRequired(); b.Property(x => x.BankName).HasMaxLength(120).IsRequired();
        b.Property(x => x.MaskedAccountNumber).HasMaxLength(40).IsRequired(); b.Property(x => x.Currency).HasMaxLength(3); b.Property(x => x.OpeningBalance).HasPrecision(18, 2);
    }
}

public class IntegrationMessageConfiguration : IEntityTypeConfiguration<IntegrationMessage>
{
    public void Configure(EntityTypeBuilder<IntegrationMessage> b)
    {
        b.ToTable("integration_messages"); b.HasKey(x => x.Id); b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Provider).HasMaxLength(80); b.Property(x => x.Operation).HasMaxLength(100); b.Property(x => x.CorrelationId).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30); b.HasIndex(x => new { x.Kind, x.ProcessedAt });
    }
}
