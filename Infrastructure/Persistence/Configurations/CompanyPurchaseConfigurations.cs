using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class CompanyPurchaseConfiguration : IEntityTypeConfiguration<CompanyPurchase>
{
    public void Configure(EntityTypeBuilder<CompanyPurchase> builder)
    {
        builder.ToTable("company_purchases", table =>
        {
            table.HasCheckConstraint("CK_company_purchases_totals", "\"Subtotal\" >= 0 AND \"TaxAmount\" >= 0 AND \"TotalAmount\" > 0 AND \"AmountPaid\" >= 0 AND \"AmountPaid\" <= \"TotalAmount\"");
            table.HasCheckConstraint("CK_company_purchases_due_date", "\"DueDate\" IS NULL OR \"DueDate\" >= \"PurchaseDate\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PurchaseNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.PurchaseNumber).IsUnique();
        builder.Property(x => x.VendorName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReceivingStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.BranchCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SupplierReference).HasMaxLength(120).IsRequired();
        builder.Property(x => x.DecisionReason).HasMaxLength(1000);
        builder.Ignore(x => x.BalanceDue);
        builder.HasIndex(x => new { x.Status, x.PurchaseDate });
        builder.HasIndex(x => new { x.ReceivingStatus, x.PurchaseDate });
        builder.HasIndex(x => new { x.Category, x.PurchaseDate });
        builder.HasIndex(x => new { x.Type, x.PurchaseDate });
        builder.HasIndex(x => new { x.BranchCode, x.Status, x.PurchaseDate });
        builder.HasIndex(x => new { x.SupplierId, x.PurchaseDate });
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CompanyPurchaseLineConfiguration : IEntityTypeConfiguration<CompanyPurchaseLine>
{
    public void Configure(EntityTypeBuilder<CompanyPurchaseLine> builder)
    {
        builder.ToTable("company_purchase_lines", table =>
        {
            table.HasCheckConstraint("CK_company_purchase_lines_quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_company_purchase_lines_received", "\"QuantityReceived\" >= 0 AND \"QuantityReceived\" <= \"Quantity\"");
            table.HasCheckConstraint("CK_company_purchase_lines_price", "\"UnitPrice\" >= 0");
            table.HasCheckConstraint("CK_company_purchase_lines_tax", "\"TaxRate\" >= 0 AND \"TaxRate\" <= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.QuantityReceived).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.TaxRate).HasPrecision(8, 4);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasIndex(x => x.CompanyPurchaseId);
        builder.HasIndex(x => x.InventoryItemId);
        builder.HasOne<CompanyPurchase>().WithMany().HasForeignKey(x => x.CompanyPurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchasePayoutConfiguration : IEntityTypeConfiguration<PurchasePayout>
{
    public void Configure(EntityTypeBuilder<PurchasePayout> builder)
    {
        builder.ToTable("purchase_payouts", table =>
            table.HasCheckConstraint("CK_purchase_payouts_amount", "\"Amount\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PayoutNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.PayoutNumber).IsUnique();
        builder.Property(x => x.Reference).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.SettlementAccountCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.CompanyPurchaseId, x.PaidAt });
        builder.HasIndex(x => x.FinancialTransactionId).IsUnique();
        builder.HasIndex(x => x.TransactionDocumentId).IsUnique();
        builder.HasOne<CompanyPurchase>().WithMany().HasForeignKey(x => x.CompanyPurchaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialTransaction>().WithOne().HasForeignKey<PurchasePayout>(x => x.FinancialTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TransactionDocument>().WithOne().HasForeignKey<PurchasePayout>(x => x.TransactionDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.SetNull);
    }
}
