using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ShipmentContainerConfiguration : IEntityTypeConfiguration<ShipmentContainer>
{
    public void Configure(EntityTypeBuilder<ShipmentContainer> b)
    {
        b.ToTable("shipment_containers"); b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        b.Property(x => x.ContainerNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(120);
        b.HasIndex(x => new { x.Provider, x.ContainerNumber }).IsUnique();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.CarrierBookingReference).HasMaxLength(100);
        b.Property(x => x.BillOfLadingNumber).HasMaxLength(100);
        b.Property(x => x.Status).HasMaxLength(80).IsRequired();
        b.Property(x => x.CurrentLocation).HasMaxLength(200);
        b.HasIndex(x => new { x.IsActive, x.LastEventAt });
        b.HasIndex(x => new { x.ShipmentMovementId, x.Type, x.IsActive });
        b.HasOne<ShippingBatch>().WithMany().HasForeignKey(x => x.ShippingBatchId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<ShipmentMovement>().WithMany().HasForeignKey(x => x.ShipmentMovementId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class CarrierTrackingEventConfiguration : IEntityTypeConfiguration<CarrierTrackingEvent>
{
    public void Configure(EntityTypeBuilder<CarrierTrackingEvent> b)
    {
        b.ToTable("carrier_tracking_events"); b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        b.Property(x => x.ExternalEventId).HasMaxLength(160).IsRequired();
        b.HasIndex(x => new { x.Provider, x.ExternalEventId }).IsUnique();
        b.Property(x => x.EventClassifier).HasMaxLength(40).IsRequired();
        b.Property(x => x.EventTypeCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.EventStatus).HasMaxLength(20).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.RawPayloadJson).HasColumnType("jsonb").IsRequired();
        b.HasOne<ShipmentContainer>().WithMany().HasForeignKey(x => x.ShipmentContainerId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ShipmentContainerId, x.OccurredAt });
    }
}
