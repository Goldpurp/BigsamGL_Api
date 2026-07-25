using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class StaffActivityLogConfiguration : IEntityTypeConfiguration<StaffActivityLog>
{
    public void Configure(EntityTypeBuilder<StaffActivityLog> builder)
    {
        builder.ToTable("staff_activity_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StaffName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.StaffEmail).HasMaxLength(254).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EmployeeNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BranchCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ActivityType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.HttpMethod).HasMaxLength(12).IsRequired();
        builder.Property(x => x.RouteTemplate).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.QueryString).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Controller).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ActionName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserAgent).HasMaxLength(600).IsRequired();
        builder.Property(x => x.DeviceId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SessionId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LocationSource).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.LocationFreshness).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.Property(x => x.LocationAccuracyMeters).HasPrecision(12, 2);
        builder.HasIndex(x => new { x.UserAccountId, x.OccurredAt });
        builder.HasIndex(x => new { x.BranchCode, x.OccurredAt });
        builder.HasIndex(x => new { x.Outcome, x.OccurredAt });
        builder.HasIndex(x => new { x.RouteTemplate, x.OccurredAt });
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => new { x.LocationFreshness, x.OccurredAt });
        builder.HasIndex(x => new { x.OccurredAt, x.Id });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffProfileId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<StaffLocationPing>().WithMany().HasForeignKey(x => x.LocationPingId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class StaffLocationPingConfiguration : IEntityTypeConfiguration<StaffLocationPing>
{
    public void Configure(EntityTypeBuilder<StaffLocationPing> builder)
    {
        builder.ToTable("staff_location_pings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StaffName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.EmployeeNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BranchCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Latitude).HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasPrecision(9, 6);
        builder.Property(x => x.AccuracyMeters).HasPrecision(12, 2);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DeviceId).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.UserAccountId, x.CapturedAt });
        builder.HasIndex(x => new { x.BranchCode, x.CapturedAt });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffProfileId).OnDelete(DeleteBehavior.SetNull);
    }
}
