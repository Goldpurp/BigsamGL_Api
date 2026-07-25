using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class OperationalManifestConfiguration : IEntityTypeConfiguration<OperationalManifest>
{
    public void Configure(EntityTypeBuilder<OperationalManifest> builder)
    {
        builder.ToTable("operational_manifests", table =>
        {
            table.HasCheckConstraint("CK_operational_manifests_logistics_method",
                "\"LogisticsMethod\" IN ('Air','Ship')");
            table.HasCheckConstraint("CK_operational_manifests_shipment_id",
                "length(btrim(\"ShipmentId\")) > 0");
            table.HasCheckConstraint("CK_operational_manifests_booking_id",
                "length(btrim(\"ShipmentBookingId\")) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BranchId).IsRequired();
        builder.Property(x => x.ManifestNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.ManifestNumber).IsUnique();
        builder.Property(x => x.ShipmentId).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.ShipmentId).IsUnique();
        builder.Property(x => x.ShipmentBookingId).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.ShipmentBookingId).IsUnique();
        builder.Property(x => x.BookingReference).HasMaxLength(120);
        builder.Property(x => x.RouteCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.RouteName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OriginCountryCode).HasMaxLength(5).IsRequired();
        builder.Property(x => x.OriginCity).HasMaxLength(160).IsRequired();
        builder.Property(x => x.DestinationCountryCode).HasMaxLength(5).IsRequired();
        builder.Property(x => x.DestinationCity).HasMaxLength(160).IsRequired();
        builder.Property(x => x.CarrierName).HasMaxLength(200);
        builder.Property(x => x.CarrierReference).HasMaxLength(200);
        builder.Property(x => x.TrackingProvider).HasMaxLength(80);
        builder.Property(x => x.ContainerOrFlightNumber).HasMaxLength(120);
        builder.Property(x => x.BillOfLadingNumber).HasMaxLength(120);
        builder.Property(x => x.FlightNumber).HasMaxLength(120);
        builder.Property(x => x.VesselName).HasMaxLength(200);
        builder.Property(x => x.VoyageNumber).HasMaxLength(120);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.Property(x => x.LogisticsMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.ManifestDate, x.LogisticsMethod });
        builder.HasIndex(x => new { x.OriginCountryCode, x.DestinationCountryCode,
            x.LogisticsMethod, x.ManifestDate });
        builder.HasIndex(x => new { x.BranchId, x.LogisticsMethod, x.OriginCountryCode,
            x.OriginCity, x.DestinationCountryCode, x.DestinationCity, x.ManifestDate });
        builder.HasIndex(x => new { x.ServiceRouteId, x.ManifestDate });
        builder.HasOne<ServiceRoute>().WithMany().HasForeignKey(x => x.ServiceRouteId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.LastEditedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OperationalManifestEntryConfiguration : IEntityTypeConfiguration<OperationalManifestEntry>
{
    public void Configure(EntityTypeBuilder<OperationalManifestEntry> builder)
    {
        builder.ToTable("operational_manifest_entries", table =>
        {
            table.HasCheckConstraint("CK_manifest_entry_quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_manifest_entry_actual_weight", "\"ActualWeightKg\" > 0");
            table.HasCheckConstraint("CK_manifest_entry_cost_weight",
                "\"CostWeightKg\" >= \"ActualWeightKg\"");
            table.HasCheckConstraint("CK_manifest_entry_amount", "\"Amount\" >= 0");
            table.HasCheckConstraint("CK_manifest_entry_referral_points", "\"ReferralPointsAwarded\" >= 0");
            table.HasCheckConstraint("CK_manifest_entry_handover_method",
                "\"HandoverMethod\" IN ('HubCollection','SentOut')");
            table.HasCheckConstraint("CK_manifest_entry_sender",
                "length(btrim(\"SenderName\")) > 0");
            table.HasCheckConstraint("CK_manifest_entry_description",
                "length(btrim(\"PackageDescription\")) > 0");
            table.HasCheckConstraint("CK_manifest_entry_currency",
                "length(\"Currency\") = 3");
            table.HasCheckConstraint("CK_manifest_entry_receiver_name",
                "length(btrim(\"ReceiverName\")) > 0");
            table.HasCheckConstraint("CK_manifest_entry_charge_basis",
                "\"ChargeBasis\" IN ('AirWeightAndDimensions','SeaBoxesAndItems','Negotiated')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceNumberSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReferrerNameSnapshot).HasMaxLength(255);
        builder.Property(x => x.CustomerNameSnapshot).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CustomerPhoneSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CustomerEmailSnapshot).HasMaxLength(254).IsRequired();
        builder.Property(x => x.ReceiverName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ReceiverPhone).HasMaxLength(40);
        builder.Property(x => x.ReceiverEmail).HasMaxLength(254);
        builder.Property(x => x.TrackingNumberSnapshot).HasMaxLength(100);
        builder.Property(x => x.SenderName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.PackageDescription).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Contents).HasMaxLength(2000);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.ActualWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.CostWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.PaymentType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ChargeBasis).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.NegotiationReference).HasMaxLength(200);
        builder.Property(x => x.HandoverMethod).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.HandoverAddress).HasMaxLength(1000);
        builder.Property(x => x.HandoverCity).HasMaxLength(160);
        builder.Property(x => x.MovementCodeSnapshot).HasMaxLength(60);
        builder.Property(x => x.ContainerNumberSnapshot).HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ManifestId, x.CargoItemId }).IsUnique()
            .HasFilter("\"CargoItemId\" IS NOT NULL");
        builder.HasIndex(x => new { x.ManifestId, x.AddedAt });
        builder.HasIndex(x => new { x.ManifestId, x.CustomerId, x.AddedAt });
        builder.HasIndex(x => new { x.ManifestId, x.InvoiceNumberSnapshot });
        builder.HasIndex(x => new { x.ShipmentMovementId, x.ShipmentContainerId });
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.ReferrerCustomerId);
        builder.HasIndex(x => x.InvoiceId).IsUnique();
        builder.HasIndex(x => x.CustomerShipmentId).IsUnique()
            .HasFilter("\"CustomerShipmentId\" IS NOT NULL");
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => x.CargoItemId);
        builder.HasIndex(x => x.ShippingBatchId);
        builder.HasIndex(x => x.ShipmentContainerId);
        builder.HasIndex(x => x.AddedByUserId);
        builder.HasOne<OperationalManifest>().WithMany().HasForeignKey(x => x.ManifestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.ReferrerCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerShipment>().WithMany().HasForeignKey(x => x.CustomerShipmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CargoItem>().WithMany().HasForeignKey(x => x.CargoItemId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ShippingBatch>().WithMany().HasForeignKey(x => x.ShippingBatchId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ShipmentMovement>().WithMany().HasForeignKey(x => x.ShipmentMovementId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ShipmentContainer>().WithMany().HasForeignKey(x => x.ShipmentContainerId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AddedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OperationalManifestChargeLineConfiguration
    : IEntityTypeConfiguration<OperationalManifestChargeLine>
{
    public void Configure(EntityTypeBuilder<OperationalManifestChargeLine> builder)
    {
        builder.ToTable("operational_manifest_charge_lines", table =>
        {
            table.HasCheckConstraint("CK_manifest_charge_line_type",
                "\"Type\" IN ('AirWeight','SeaBox','SeaItem','Negotiated')");
            table.HasCheckConstraint("CK_manifest_charge_line_description",
                "length(btrim(\"Description\")) > 0");
            table.HasCheckConstraint("CK_manifest_charge_line_quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_manifest_charge_line_unit_price", "\"UnitPrice\" > 0");
            table.HasCheckConstraint("CK_manifest_charge_line_amount", "\"LineAmount\" > 0");
            table.HasCheckConstraint("CK_manifest_charge_line_sort", "\"SortOrder\" >= 0");
            table.HasCheckConstraint("CK_manifest_charge_line_dimensions",
                "(\"LengthCm\" IS NULL OR \"LengthCm\" > 0) AND " +
                "(\"WidthCm\" IS NULL OR \"WidthCm\" > 0) AND " +
                "(\"HeightCm\" IS NULL OR \"HeightCm\" > 0)");
            table.HasCheckConstraint("CK_manifest_charge_line_weights",
                "(\"ActualWeightKg\" IS NULL OR \"ActualWeightKg\" > 0) AND " +
                "(\"VolumetricDivisor\" IS NULL OR \"VolumetricDivisor\" > 0) AND " +
                "(\"VolumetricWeightKg\" IS NULL OR \"VolumetricWeightKg\" > 0) AND " +
                "(\"BillableWeightKg\" IS NULL OR \"BillableWeightKg\" > 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.BoxSize).HasMaxLength(120);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LengthCm).HasPrecision(18, 3);
        builder.Property(x => x.WidthCm).HasPrecision(18, 3);
        builder.Property(x => x.HeightCm).HasPrecision(18, 3);
        builder.Property(x => x.ActualWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.VolumetricDivisor).HasPrecision(18, 3);
        builder.Property(x => x.VolumetricWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.BillableWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.LineAmount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.ManifestEntryId, x.SortOrder });
        builder.HasOne<OperationalManifestEntry>().WithMany().HasForeignKey(x => x.ManifestEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
