using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.ToTable("credit_notes", table =>
        {
            table.HasCheckConstraint("CK_credit_notes_amount", "\"OriginalAmount\" > 0");
            table.HasCheckConstraint("CK_credit_notes_used", "\"UsedAmount\" >= 0 AND \"UsedAmount\" <= \"OriginalAmount\"");
            table.HasCheckConstraint("CK_credit_notes_currency", "char_length(\"Currency\") = 3");
            table.HasCheckConstraint("CK_credit_notes_reason", "length(btrim(\"Reason\")) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreditNoteNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.CreditNoteNumber).IsUnique();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.OriginalAmount).HasPrecision(18, 2);
        builder.Property(x => x.UsedAmount).HasPrecision(18, 2);
        builder.Ignore(x => x.AvailableAmount);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.VoidReason).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => new { x.BranchId, x.Currency, x.State, x.IssuedAt });
        builder.HasIndex(x => new { x.CustomerId, x.Currency, x.State, x.IssuedAt });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.OriginalInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CreditApplicationConfiguration : IEntityTypeConfiguration<CreditApplication>
{
    public void Configure(EntityTypeBuilder<CreditApplication> builder)
    {
        builder.ToTable("credit_applications", table =>
            table.HasCheckConstraint("CK_credit_applications_amount", "\"Amount\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.AppliedByUserId, x.IdempotencyKey, x.CreditNoteId }).IsUnique();
        builder.HasIndex(x => new { x.CreditNoteId, x.AppliedAt });
        builder.HasIndex(x => new { x.InvoiceId, x.AppliedAt });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditNote>().WithMany().HasForeignKey(x => x.CreditNoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
