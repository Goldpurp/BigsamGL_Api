using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ShippingPriceCatalogueItemConfiguration : IEntityTypeConfiguration<ShippingPriceCatalogueItem>
{
    public void Configure(EntityTypeBuilder<ShippingPriceCatalogueItem> builder)
    {
        builder.ToTable("shipping_price_catalogue", table =>
        {
            table.HasCheckConstraint("CK_shipping_price_currency", "char_length(\"Currency\") = 3");
            table.HasCheckConstraint("CK_shipping_price_values",
                "(\"LowerPrice\" IS NULL OR \"LowerPrice\" >= 0) AND " +
                "(\"UpperPrice\" IS NULL OR \"UpperPrice\" >= \"LowerPrice\") AND " +
                "(\"HandlingFee\" IS NULL OR \"HandlingFee\" >= 0) AND " +
                "(\"DocumentationFee\" IS NULL OR \"DocumentationFee\" >= 0) AND " +
                "(\"MinimumCharge\" IS NULL OR \"MinimumCharge\" >= 0) AND " +
                "(\"MinimumQuantity\" IS NULL OR \"MinimumQuantity\" >= 0) AND " +
                "(\"MaximumQuantity\" IS NULL OR \"MaximumQuantity\" >= \"MinimumQuantity\")");
            table.HasCheckConstraint("CK_shipping_price_kind_values",
                "(\"PriceKind\" = 'CustomQuote' AND \"LowerPrice\" IS NULL AND \"UpperPrice\" IS NULL) OR " +
                "(\"PriceKind\" IN ('Fixed','From') AND \"LowerPrice\" IS NOT NULL) OR " +
                "(\"PriceKind\" = 'Range' AND \"LowerPrice\" IS NOT NULL AND \"UpperPrice\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceCode).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NormalizedCode).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.NormalizedCode).IsUnique();
        builder.Property(x => x.Category).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Group).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ServiceArea).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DestinationCountryCodes).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DestinationZone).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PriceKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.LowerPrice).HasPrecision(18, 2);
        builder.Property(x => x.UpperPrice).HasPrecision(18, 2);
        builder.Property(x => x.HandlingFee).HasPrecision(18, 2);
        builder.Property(x => x.DocumentationFee).HasPrecision(18, 2);
        builder.Property(x => x.MinimumCharge).HasPrecision(18, 2);
        builder.Property(x => x.MinimumQuantity).HasPrecision(18, 3);
        builder.Property(x => x.MaximumQuantity).HasPrecision(18, 3);
        builder.Property(x => x.DispatchSchedule).HasMaxLength(300);
        builder.Property(x => x.EstimatedDelivery).HasMaxLength(300);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => new { x.BranchId, x.State, x.Method, x.Category });
        builder.HasIndex(x => new { x.BranchId, x.ServiceCode, x.DisplayOrder });
        builder.HasIndex(x => new { x.IsPublic, x.State, x.Method });
        builder.HasIndex(x => new { x.BranchId, x.NormalizedName });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceRoute>().WithMany().HasForeignKey(x => x.ServiceRouteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ShippingPriceHistoryConfiguration : IEntityTypeConfiguration<ShippingPriceHistory>
{
    public void Configure(EntityTypeBuilder<ShippingPriceHistory> builder)
    {
        builder.ToTable("shipping_price_history");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => new { x.ShippingPriceCatalogueItemId, x.Version }).IsUnique();
        builder.HasOne<ShippingPriceCatalogueItem>().WithMany()
            .HasForeignKey(x => x.ShippingPriceCatalogueItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
