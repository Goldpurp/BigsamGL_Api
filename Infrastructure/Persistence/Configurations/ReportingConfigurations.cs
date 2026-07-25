using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ReportExportJobConfiguration : IEntityTypeConfiguration<ReportExportJob>
{
    public void Configure(EntityTypeBuilder<ReportExportJob> builder)
    {
        builder.ToTable("report_export_jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReportKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.FiltersJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(180).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(x => new { x.RequestedByUserId, x.CreatedAt });
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
