using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class StaffNotificationConfiguration : IEntityTypeConfiguration<StaffNotification>
{
    public void Configure(EntityTypeBuilder<StaffNotification> builder)
    {
        builder.ToTable("staff_notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1200).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(300);
        builder.HasIndex(x => new { x.UserAccountId, x.ReadAt, x.CreatedAt });
        builder.HasIndex(x => new { x.BranchId, x.CreatedAt });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class UserNotificationPreferenceConfiguration :
    IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("user_notification_preferences");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserAccountId).IsUnique();
        builder.HasOne<UserAccount>().WithOne().HasForeignKey<UserNotificationPreference>(
            x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
