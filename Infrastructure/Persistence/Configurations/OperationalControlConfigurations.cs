using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ScheduledPickupConfiguration : IEntityTypeConfiguration<ScheduledPickup>
{
    public void Configure(EntityTypeBuilder<ScheduledPickup> builder)
    {
        builder.ToTable("scheduled_pickups"); builder.HasKey(x => x.Id);
        builder.Property(x => x.PickupNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.PickupNumber).IsUnique();
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.Property(x => x.ContactName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.City).HasMaxLength(120).IsRequired();
        builder.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
        builder.Property(x => x.GoodsType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Contents).HasMaxLength(2000);
        builder.Property(x => x.HandlingNote).HasMaxLength(2000);
        builder.Property(x => x.ActualWeightKg).HasPrecision(15, 3);
        builder.Property(x => x.ChargeableWeightKg).HasPrecision(15, 3);
        builder.Property(x => x.BoxSizesJson).HasColumnType("jsonb");
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PaymentReference).HasMaxLength(120);
        builder.Property(x => x.PaymentState).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.AssignedTeam).HasMaxLength(160);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.Property(x => x.DeleteReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.BranchId, x.ScheduledAt, x.State });
        builder.HasIndex(x => new { x.BranchId, x.Phone, x.ScheduledAt });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AssignedDriverUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<FinancialTransaction>().WithMany().HasForeignKey(x => x.FinancialTransactionId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<TransactionDocument>().WithMany().HasForeignKey(x => x.ReceiptDocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ManifestTrackingConnectionConfiguration : IEntityTypeConfiguration<ManifestTrackingConnection>
{
    public void Configure(EntityTypeBuilder<ManifestTrackingConnection> builder)
    {
        builder.ToTable("manifest_tracking_connections"); builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.Property(x => x.Provider).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ShippingCompanyName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ExternalReference).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ReferenceType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.PublicTokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.PublicTokenHash).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ProviderStatus).HasMaxLength(100);
        builder.Property(x => x.LastKnownPlace).HasMaxLength(200);
        builder.HasIndex(x => x.ManifestId).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.ExternalReference });
        builder.HasIndex(x => new { x.BranchId, x.Status, x.LastSuccessfulSyncAt });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OperationalManifest>().WithMany().HasForeignKey(x => x.ManifestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ManifestTrackingEventConfiguration : IEntityTypeConfiguration<ManifestTrackingEvent>
{
    public void Configure(EntityTypeBuilder<ManifestTrackingEvent> builder)
    {
        builder.ToTable("manifest_tracking_events"); builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.Property(x => x.ProviderEventId).HasMaxLength(160);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ReviewState).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.InternalTitle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InternalDescription).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ProviderStatus).HasMaxLength(100);
        builder.Property(x => x.RawPayloadProtected).HasColumnType("jsonb");
        builder.Property(x => x.PublicTitle).HasMaxLength(200);
        builder.Property(x => x.PublicDescription).HasMaxLength(2000);
        builder.Property(x => x.Place).HasMaxLength(200);
        builder.HasIndex(x => new { x.ConnectionId, x.ProviderEventId }).IsUnique()
            .HasFilter("\"ProviderEventId\" IS NOT NULL");
        builder.HasIndex(x => new { x.BranchId, x.ReviewState, x.OccurredAt });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ManifestTrackingConnection>().WithMany().HasForeignKey(x => x.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CargoItemIssueConfiguration : IEntityTypeConfiguration<CargoItemIssue>
{
    public void Configure(EntityTypeBuilder<CargoItemIssue> builder)
    {
        builder.ToTable("cargo_item_issues"); builder.HasKey(x => x.Id);
        builder.Property(x => x.IssueNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.IssueNumber).IsUnique();
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50);
        builder.Property(x => x.ExpectedDetails).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.FoundDetails).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.LastSeenPlace).HasMaxLength(200);
        builder.Property(x => x.InternalNote).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Resolution).HasMaxLength(4000);
        builder.HasIndex(x => new { x.BranchId, x.State, x.Severity, x.CreatedAt });
        builder.HasIndex(x => x.InvoiceNumber);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerShipment>().WithMany().HasForeignKey(x => x.CustomerShipmentId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<OperationalManifest>().WithMany().HasForeignKey(x => x.ManifestId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<OperationalManifestEntry>().WithMany().HasForeignKey(x => x.ManifestLineId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class CargoItemIssueEvidenceConfiguration : IEntityTypeConfiguration<CargoItemIssueEvidence>
{
    public void Configure(EntityTypeBuilder<CargoItemIssueEvidence> builder)
    {
        builder.ToTable("cargo_item_issue_evidence"); builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.CargoItemIssueId, x.Sha256 }).IsUnique();
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CargoItemIssue>().WithMany().HasForeignKey(x => x.CargoItemIssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ShipmentDocument>().WithMany().HasForeignKey(x => x.ShipmentDocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
