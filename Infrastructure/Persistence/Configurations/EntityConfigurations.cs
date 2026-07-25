using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.FullName).HasMaxLength(255).IsRequired();
        builder.Property(c => c.NormalizedFullName).HasMaxLength(255).IsRequired();
        builder.HasIndex(c => c.NormalizedFullName).IsUnique();
        builder.Property(c => c.ClientNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(c => c.ClientNumber).IsUnique();
        builder.Property(c => c.BranchId).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(255);
        builder.Property(c => c.NormalizedEmail).HasMaxLength(255);
        builder.HasIndex(c => c.NormalizedEmail).IsUnique().HasFilter("\"NormalizedEmail\" IS NOT NULL");
        builder.Property(c => c.WalletBalance).HasColumnType("decimal(15,2)").HasDefaultValue(0.00m);
        builder.Property(c => c.WalletCurrency).HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(c => c.CountryCode).HasMaxLength(5).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(40).HasDefaultValue("");
        builder.Property(c => c.NormalizedPhone).HasMaxLength(40);
        builder.HasIndex(c => c.NormalizedPhone).IsUnique().HasFilter("\"NormalizedPhone\" IS NOT NULL");
        builder.Property(c => c.CompanyName).HasMaxLength(160);
        builder.Property(c => c.AccountType).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(ClientAccountType.Individual);
        builder.HasIndex(c => new { c.AccountType, c.CreatedAt });
        builder.HasIndex(c => new { c.BranchId, c.CreatedAt });
        builder.HasIndex(c => c.MergedIntoCustomerId);
        builder.HasOne<Branch>().WithMany().HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(c => c.MergedIntoCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CustomerId).IsUnique();
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CargoItemConfiguration : IEntityTypeConfiguration<CargoItem>
{
    public void Configure(EntityTypeBuilder<CargoItem> builder)
    {
        builder.ToTable("cargo_items");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TrackingNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.TrackingNumber).IsUnique();
        builder.Property(c => c.MarkerCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.MarkerCode);
        builder.Property(c => c.Description).IsRequired();
        builder.Property(c => c.ActualWeightKg).HasColumnType("decimal(15,2)");
        builder.Property(c => c.LengthCm).HasColumnType("decimal(15,2)");
        builder.Property(c => c.WidthCm).HasColumnType("decimal(15,2)");
        builder.Property(c => c.HeightCm).HasColumnType("decimal(15,2)");
        builder.Property(c => c.VolumetricWeightKg).HasColumnType("decimal(15,2)");
        builder.Property(c => c.DeclaredValueAmount).HasColumnType("decimal(15,2)");
        builder.Property(c => c.DeclaredValueCurrency).HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(c => c.CustomsCategory).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50).HasDefaultValue(CargoStatus.Logged);
        builder.Property(c => c.ShippingCostAmount).HasColumnType("decimal(15,2)").HasDefaultValue(0.00m);
        builder.Property(c => c.ShippingCostCurrency).HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(c => c.CarrierProvider).HasMaxLength(80);
        builder.Property(c => c.ExternalTrackingNumber).HasMaxLength(120);
        builder.Property(c => c.AssignedTransportMode).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(c => new { c.CarrierProvider, c.ExternalTrackingNumber });
        builder.HasIndex(c => c.CustomerId);
        builder.HasIndex(c => c.BatchId);
        builder.HasIndex(c => c.InvoiceId);
        builder.HasIndex(c => c.ShipmentMovementId);
        builder.HasIndex(c => new { c.AssignedTransportMode, c.Status, c.CreatedAt });
        builder.HasIndex(c => c.CreatedAt);
        builder.HasIndex(c => new { c.Status, c.CreatedAt });
        builder.HasOne<ShipmentContainer>().WithMany().HasForeignKey(c => c.ShipmentContainerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(c => c.InvoiceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ShipmentMovement>().WithMany().HasForeignKey(c => c.ShipmentMovementId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ShippingBatchConfiguration : IEntityTypeConfiguration<ShippingBatch>
{
    public void Configure(EntityTypeBuilder<ShippingBatch> builder)
    {
        builder.ToTable("shipping_batches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.BatchCode).HasMaxLength(100).IsRequired();
        builder.HasIndex(b => b.BatchCode).IsUnique();
        builder.Property(b => b.AirwayBillNumber).HasMaxLength(100);
        builder.Property(b => b.MaxWeightCapacityKg).HasColumnType("decimal(15,2)");
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(50).HasDefaultValue(BatchStatus.Draft);
        builder.Property(b => b.OriginHub).HasMaxLength(150).IsRequired();
        builder.Property(b => b.DestinationHub).HasMaxLength(150).IsRequired();
        builder.Property(b => b.ConsolidationType).HasConversion<string>().HasMaxLength(40)
            .HasDefaultValue(ConsolidationType.Groupage);
        builder.HasIndex(b => new { b.ConsolidationType, b.Status, b.ScheduledReleaseDate });
        builder.Property(b => b.VesselName).HasMaxLength(200).HasDefaultValue("");
        builder.Property(b => b.VoyageNumber).HasMaxLength(100).HasDefaultValue("");
        builder.Property(b => b.PortOfLoading).HasMaxLength(200).HasDefaultValue("");
        builder.Property(b => b.PortOfDischarge).HasMaxLength(200).HasDefaultValue("");
        builder.Property(b => b.FreezeTracking).HasDefaultValue(false);
        builder.HasIndex(b => b.CreatedAt);
    }
}

public class WalletLedgerConfiguration : IEntityTypeConfiguration<WalletLedger>
{
    public void Configure(EntityTypeBuilder<WalletLedger> builder)
    {
        builder.ToTable("wallet_ledgers");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Amount).HasColumnType("decimal(15,2)");
        builder.Property(w => w.Currency).HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(w => w.TransactionType).HasConversion<string>().HasMaxLength(50);
        builder.Property(w => w.Description).IsRequired();
        builder.HasIndex(w => new { w.CustomerId, w.Timestamp });
    }
}
