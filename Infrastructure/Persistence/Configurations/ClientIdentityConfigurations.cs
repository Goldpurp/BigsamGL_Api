using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ClientDuplicateReviewConfiguration : IEntityTypeConfiguration<ClientDuplicateReview>
{
    public void Configure(EntityTypeBuilder<ClientDuplicateReview> builder)
    {
        builder.ToTable("client_duplicate_reviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InputFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SubmittedFullName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.SubmittedEmail).HasMaxLength(255).IsRequired();
        builder.Property(x => x.SubmittedPhone).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SubmittedInvoiceNumber).HasMaxLength(80).IsRequired();
        builder.Property(x => x.MatchSummaryJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.DecisionReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.InputFingerprint, x.Status, x.ExpiresAt });
        builder.HasIndex(x => x.SelectedCustomerId);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.SelectedCustomerId).OnDelete(DeleteBehavior.SetNull);
    }
}
