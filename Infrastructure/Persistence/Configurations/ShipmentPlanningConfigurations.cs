using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ShipmentMovementConfiguration : IEntityTypeConfiguration<ShipmentMovement>
{
    public void Configure(EntityTypeBuilder<ShipmentMovement> builder)
    {
        builder.ToTable("shipment_movements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MovementCode).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.MovementCode).IsUnique();
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.OperatorType).HasConversion<string>().HasMaxLength(40).IsRequired()
            .HasDefaultValue(Domain.Enums.TransportOperatorType.ThirdPartyCarrier);
        builder.Property(x => x.CarrierName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.CarrierCode).HasMaxLength(40);
        builder.Property(x => x.FlightNumber).HasMaxLength(80);
        builder.Property(x => x.VesselName).HasMaxLength(160);
        builder.Property(x => x.VoyageNumber).HasMaxLength(80);
        builder.Property(x => x.MasterTransportReference).HasMaxLength(120);
        builder.Property(x => x.OriginGateway).HasMaxLength(160);
        builder.Property(x => x.DestinationGateway).HasMaxLength(160);
        builder.Property(x => x.OriginBranchCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DestinationBranchCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Mode, x.Status, x.ScheduledDepartureAt });
        builder.HasIndex(x => new { x.CarrierName, x.ScheduledDepartureAt });
        builder.HasIndex(x => new { x.OperatorType, x.ScheduledDepartureAt });
        builder.HasIndex(x => new { x.OriginBranchCode, x.DestinationBranchCode, x.ScheduledDepartureAt });
        builder.HasIndex(x => x.ShippingBatchId);
        builder.HasOne<ShippingBatch>().WithMany().HasForeignKey(x => x.ShippingBatchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class CargoAllocationConfiguration : IEntityTypeConfiguration<CargoAllocation>
{
    public void Configure(EntityTypeBuilder<CargoAllocation> builder)
    {
        builder.ToTable("cargo_allocations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ContainerType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.CarrierName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.AssignedByUserId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UnassignedByUserId).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => x.CargoItemId).IsUnique().HasFilter("\"UnassignedAt\" IS NULL");
        builder.HasIndex(x => new { x.ShipmentMovementId, x.UnassignedAt, x.AssignedAt });
        builder.HasIndex(x => new { x.InvoiceId, x.AssignedAt });
        builder.HasIndex(x => x.ShipmentContainerId);
        builder.HasOne<CargoItem>().WithMany().HasForeignKey(x => x.CargoItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShipmentMovement>().WithMany().HasForeignKey(x => x.ShipmentMovementId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShipmentContainer>().WithMany().HasForeignKey(x => x.ShipmentContainerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class LegacyImportBatchConfiguration : IEntityTypeConfiguration<LegacyImportBatch>
{
    public void Configure(EntityTypeBuilder<LegacyImportBatch> builder)
    {
        builder.ToTable("legacy_import_batches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FileSha256).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.FileSha256).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CommittedByUserId).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(2000);
        builder.Property(x => x.MappingJson).HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

public sealed class LegacyImportRowConfiguration : IEntityTypeConfiguration<LegacyImportRow>
{
    public void Configure(EntityTypeBuilder<LegacyImportRow> builder)
    {
        builder.ToTable("legacy_import_rows");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SheetName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceKey).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DataJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000);
        builder.HasIndex(x => new { x.LegacyImportBatchId, x.EntityType, x.RowNumber }).IsUnique();
        builder.HasIndex(x => new { x.LegacyImportBatchId, x.Status });
        builder.HasOne<LegacyImportBatch>().WithMany().HasForeignKey(x => x.LegacyImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
