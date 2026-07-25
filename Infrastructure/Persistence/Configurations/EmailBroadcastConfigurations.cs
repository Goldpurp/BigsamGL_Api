using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class EmailBroadcastConfiguration : IEntityTypeConfiguration<EmailBroadcast>
{
    public void Configure(EntityTypeBuilder<EmailBroadcast> builder)
    {
        builder.ToTable("email_broadcasts", table =>
        {
            table.HasCheckConstraint("CK_email_broadcast_counts",
                "\"TotalRecipients\" >= 0 AND \"PendingRecipients\" >= 0 AND \"SentRecipients\" >= 0 AND \"FailedRecipients\" >= 0 AND \"SkippedRecipients\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CampaignNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.CampaignNumber).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(20_000).IsRequired();
        builder.Property(x => x.ReplyTo).HasMaxLength(254);
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.AudienceSelectionJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Status, x.ScheduledFor, x.CreatedAt });
        builder.HasIndex(x => new { x.Purpose, x.CreatedAt });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class EmailBroadcastRecipientConfiguration : IEntityTypeConfiguration<EmailBroadcastRecipient>
{
    public void Configure(EntityTypeBuilder<EmailBroadcastRecipient> builder)
    {
        builder.ToTable("email_broadcast_recipients", table =>
            table.HasCheckConstraint("CK_email_broadcast_recipient_attempts", "\"AttemptCount\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.ProviderReference).HasMaxLength(160);
        builder.HasIndex(x => new { x.EmailBroadcastId, x.NormalizedEmail }).IsUnique();
        builder.HasIndex(x => new { x.EmailBroadcastId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.SourceType, x.SourceId });
        builder.HasOne<EmailBroadcast>().WithMany().HasForeignKey(x => x.EmailBroadcastId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class EmailBroadcastAttachmentConfiguration : IEntityTypeConfiguration<EmailBroadcastAttachment>
{
    public void Configure(EntityTypeBuilder<EmailBroadcastAttachment> builder)
    {
        builder.ToTable("email_broadcast_attachments", table =>
            table.HasCheckConstraint("CK_email_broadcast_attachment_size", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 20000000"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Checksum).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Content).HasColumnType("bytea").IsRequired();
        builder.HasIndex(x => new { x.EmailBroadcastId, x.FileName }).IsUnique();
        builder.HasOne<EmailBroadcast>().WithMany().HasForeignKey(x => x.EmailBroadcastId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
