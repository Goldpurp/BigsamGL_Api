using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class AdditionalServicePriceConfiguration : IEntityTypeConfiguration<AdditionalServicePrice>
{
    public void Configure(EntityTypeBuilder<AdditionalServicePrice> builder)
    {
        builder.ToTable("additional_service_prices", table =>
        {
            table.HasCheckConstraint("CK_additional_service_prices_amount", "\"Amount\" >= 0");
            table.HasCheckConstraint("CK_additional_service_prices_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Unit).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.BranchCode).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.Code, x.BranchCode, x.EffectiveFrom }).IsUnique();
        builder.HasIndex(x => new { x.Code, x.BranchCode }).IsUnique().HasFilter("\"IsActive\" = TRUE");
        builder.HasIndex(x => new { x.Category, x.BranchCode, x.IsActive });
        builder.HasOne<AdditionalServicePrice>().WithMany().HasForeignKey(x => x.ReplacesPriceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

