using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("branches"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired(); b.Property(x => x.CountryCode).HasMaxLength(5).IsRequired();
        b.Property(x => x.TimeZone).HasMaxLength(80).IsRequired(); b.Property(x => x.Address).HasMaxLength(500).IsRequired();
    }
}

public class ServiceRouteConfiguration : IEntityTypeConfiguration<ServiceRoute>
{
    public void Configure(EntityTypeBuilder<ServiceRoute> b)
    {
        b.ToTable("service_routes"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired(); b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DestinationCountryCode).HasMaxLength(5).IsRequired();
        b.Property(x => x.DestinationCity).HasMaxLength(160).IsRequired();
        b.Property(x => x.DestinationRegion).HasMaxLength(160);
        b.Property(x => x.EstimatedTimeline).HasMaxLength(300).IsRequired();
        b.Property(x => x.VolumetricDivisor).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.OriginBranchId, x.DestinationCountryCode,
            x.DestinationCity, x.Mode, x.IsActive });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.OriginBranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.DestinationBranchId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class RateCardConfiguration : IEntityTypeConfiguration<RateCard>
{
    public void Configure(EntityTypeBuilder<RateCard> b)
    {
        b.ToTable("rate_cards"); b.HasKey(x => x.Id);
        b.Property(x => x.CargoCategory).HasMaxLength(100).IsRequired(); b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.MinimumWeightKg).HasPrecision(18, 2); b.Property(x => x.RatePerKg).HasPrecision(18, 2);
        b.Property(x => x.MinimumCharge).HasPrecision(18, 2); b.Property(x => x.FuelSurchargeRate).HasPrecision(8, 4);
        b.Property(x => x.CustomsRate).HasPrecision(8, 4); b.HasIndex(x => new { x.ServiceRouteId, x.CargoCategory, x.EffectiveFrom });
        b.HasOne<ServiceRoute>().WithMany().HasForeignKey(x => x.ServiceRouteId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> b)
    {
        b.ToTable("customer_addresses"); b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30); b.Property(x => x.ContactName).HasMaxLength(160).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(40).IsRequired(); b.Property(x => x.Line1).HasMaxLength(250).IsRequired();
        b.Property(x => x.Line2).HasMaxLength(250); b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.State).HasMaxLength(100); b.Property(x => x.PostalCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.CountryCode).HasMaxLength(5).IsRequired(); b.HasIndex(x => new { x.CustomerId, x.Type });
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShippingQuoteConfiguration : IEntityTypeConfiguration<ShippingQuote>
{
    public void Configure(EntityTypeBuilder<ShippingQuote> b)
    {
        b.ToTable("shipping_quotes"); b.HasKey(x => x.Id);
        b.Property(x => x.QuoteNumber).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.QuoteNumber).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30); b.Property(x => x.ContactName).HasMaxLength(160).IsRequired();
        b.Property(x => x.ContactEmail).HasMaxLength(254).IsRequired(); b.Property(x => x.ContactPhone).HasMaxLength(40);
        b.Property(x => x.CargoDescription).HasMaxLength(500).IsRequired(); b.Property(x => x.CargoCategory).HasMaxLength(100).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        foreach (var property in new[] { nameof(ShippingQuote.ActualWeightKg), nameof(ShippingQuote.VolumetricWeightKg), nameof(ShippingQuote.BillableWeightKg), nameof(ShippingQuote.BaseAmount), nameof(ShippingQuote.FuelSurchargeAmount), nameof(ShippingQuote.CustomsAmount), nameof(ShippingQuote.TotalAmount) })
            b.Property<decimal>(property).HasPrecision(18, 2);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.CustomerId, x.CreatedAt });
        b.HasOne<ServiceRoute>().WithMany().HasForeignKey(x => x.ServiceRouteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SalesLead>().WithMany().HasForeignKey(x => x.SalesLeadId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CargoPieceConfiguration : IEntityTypeConfiguration<CargoPiece>
{
    public void Configure(EntityTypeBuilder<CargoPiece> b)
    {
        b.ToTable("cargo_pieces"); b.HasKey(x => x.Id); b.Property(x => x.PieceNumber).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.PieceNumber).IsUnique(); b.Property(x => x.ActualWeightKg).HasPrecision(18, 2);
        b.Property(x => x.LengthCm).HasPrecision(18, 2); b.Property(x => x.WidthCm).HasPrecision(18, 2); b.Property(x => x.HeightCm).HasPrecision(18, 2);
        b.Property(x => x.WarehouseLocation).HasMaxLength(100); b.Property(x => x.Condition).HasMaxLength(300);
        b.HasOne<CargoItem>().WithMany().HasForeignKey(x => x.CargoItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShipmentMilestoneConfiguration : IEntityTypeConfiguration<ShipmentMilestone>
{
    public void Configure(EntityTypeBuilder<ShipmentMilestone> b)
    {
        b.ToTable("shipment_milestones"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40)
            .HasDefaultValue(ShipmentEventType.General).ValueGeneratedNever();
        b.Property(x => x.Title).HasMaxLength(160).IsRequired(); b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.Location).HasMaxLength(160); b.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Source).HasMaxLength(80); b.Property(x => x.ExternalEventId).HasMaxLength(160);
        b.Property(x => x.CarrierProvider).HasMaxLength(80); b.Property(x => x.IssueCode).HasMaxLength(80);
        b.Property(x => x.RevisionReason).HasMaxLength(500); b.HasIndex(x => new { x.CargoItemId, x.OccurredAt });
        b.HasIndex(x => new { x.CargoItemId, x.CarrierProvider, x.ExternalEventId }).IsUnique()
            .HasFilter("\"ExternalEventId\" IS NOT NULL");
        b.HasOne<CargoItem>().WithMany().HasForeignKey(x => x.CargoItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShipmentDocumentConfiguration : IEntityTypeConfiguration<ShipmentDocument>
{
    public void Configure(EntityTypeBuilder<ShipmentDocument> b)
    {
        b.ToTable("shipment_documents"); b.HasKey(x => x.Id); b.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired(); b.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired(); b.Property(x => x.Checksum).HasMaxLength(128).IsRequired();
        b.HasOne<CargoItem>().WithMany().HasForeignKey(x => x.CargoItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}
