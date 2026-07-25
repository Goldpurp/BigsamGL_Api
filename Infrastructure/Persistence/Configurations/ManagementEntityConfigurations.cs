using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.NormalizedFullName).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => x.NormalizedFullName).IsUnique();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.CustomerId).IsUnique();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserAccountId, x.ExpiresAt });
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
    }
}

public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("staff_profiles");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserAccountId).IsUnique();
        builder.Property(x => x.EmployeeNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.EmployeeNumber).IsUnique();
        builder.Property(x => x.JobTitle).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Department).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BranchCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.StatusReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.BranchCode, x.Status });
        builder.HasOne<UserAccount>().WithOne().HasForeignKey<StaffProfile>(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChangesJson).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => new { x.ActorId, x.CreatedAt });
        builder.HasIndex(x => new { x.CreatedAt, x.Id });
    }
}

public class SalesLeadConfiguration : IEntityTypeConfiguration<SalesLead>
{
    public void Configure(EntityTypeBuilder<SalesLead> builder)
    {
        builder.ToTable("sales_leads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContactName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ContactEmail).HasMaxLength(254);
        builder.Property(x => x.ContactPhone).HasMaxLength(40);
        builder.Property(x => x.CompanyName).HasMaxLength(160);
        builder.Property(x => x.ServiceInterest).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Origin).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Destination).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Stage).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EstimatedValue).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.LostReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Stage, x.NextActionAt });
        builder.HasIndex(x => new { x.IsArchived, x.Priority, x.NextActionAt });
        builder.HasIndex(x => new { x.Stage, x.CreatedAt });
        builder.HasIndex(x => x.OwnerUserId);
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BranchId).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.Property(x => x.CustomerName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.IssuerBranchCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.PreferredPaymentMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.VerificationCode).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.VerificationCode).IsUnique();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.AppliedCreditAmount).HasPrecision(18, 2);
        builder.Ignore(x => x.BalanceDue);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Status, x.DueDate });
        builder.HasIndex(x => new { x.Status, x.IssueDate });
        builder.HasIndex(x => new { x.Currency, x.IssueDate });
        builder.HasIndex(x => new { x.Type, x.IssueDate });
        builder.HasIndex(x => new { x.PaymentStatus, x.IssueDate });
        builder.HasIndex(x => new { x.IssuerBranchCode, x.IssueDate });
        builder.HasIndex(x => new { x.CustomerId, x.IssueDate });
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.OriginalInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.TaxRate).HasPrecision(8, 4);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasIndex(x => x.InvoiceId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BranchId).IsRequired();
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.SettlementAccountCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.InvoiceId, x.PaidAt });
        builder.HasIndex(x => x.BankAccountId);
        builder.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExpenseNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.ExpenseNumber).IsUnique();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Payee).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.BranchCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.Status, x.IncurredAt });
    }
}
