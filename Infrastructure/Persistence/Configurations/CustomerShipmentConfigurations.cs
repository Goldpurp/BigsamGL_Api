using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class CustomerShipmentConfiguration : IEntityTypeConfiguration<CustomerShipment>
{
    public void Configure(EntityTypeBuilder<CustomerShipment> builder)
    {
        builder.ToTable("customer_shipments", table =>
        {
            table.HasCheckConstraint("CK_customer_shipments_invoice_number",
                "\"InvoiceNumber\" ~ '^BGL[0-9]{4,}INV$'");
            table.HasCheckConstraint("CK_customer_shipments_method", "\"Method\" IN ('Air','Sea')");
            table.HasCheckConstraint("CK_customer_shipments_handover", "\"HandoverMethod\" IN ('Pickup','Delivery')");
            table.HasCheckConstraint("CK_customer_shipments_goods_type", "\"GoodsType\" IN ('Fragile','NonFragile')");
            table.HasCheckConstraint("CK_customer_shipments_pricing",
                "\"PricingMethod\" IN ('Weight','BoxSizesAndItems','NegotiatedPrice')");
            table.HasCheckConstraint("CK_customer_shipments_piece_count", "\"PieceCount\" > 0");
            table.HasCheckConstraint("CK_customer_shipments_weights",
                "(\"ActualWeightKg\" IS NULL OR \"ActualWeightKg\" > 0) AND " +
                "(\"ChargeableWeightKg\" IS NULL OR \"ChargeableWeightKg\" >= \"ActualWeightKg\")");
            table.HasCheckConstraint("CK_customer_shipments_delivery_address",
                "\"HandoverMethod\" <> 'Delivery' OR length(btrim(coalesce(\"DeliveryAddress\", ''))) > 0");
            table.HasCheckConstraint("CK_customer_shipments_row_version", "\"RowVersion\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.Property(x => x.InvoiceNumericFragment).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.InvoiceNumericFragment);
        builder.Property(x => x.ReceiverName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.OriginCountryCode).HasMaxLength(5).IsRequired();
        builder.Property(x => x.OriginCity).HasMaxLength(160).IsRequired();
        builder.Property(x => x.DestinationCountryCode).HasMaxLength(5).IsRequired();
        builder.Property(x => x.DestinationCityOrState).HasMaxLength(160).IsRequired();
        builder.Property(x => x.DeliveryAddress).HasMaxLength(1000);
        builder.Property(x => x.HandoverMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.GoodsType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Contents).HasMaxLength(2000);
        builder.Property(x => x.PricingMethod).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ServiceCodeSnapshot).HasMaxLength(80);
        builder.Property(x => x.ActualWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.ChargeableWeightKg).HasPrecision(18, 3);
        builder.Property(x => x.LengthCm).HasPrecision(18, 2);
        builder.Property(x => x.WidthCm).HasPrecision(18, 2);
        builder.Property(x => x.HeightCm).HasPrecision(18, 2);
        builder.Property(x => x.NegotiatedReason).HasMaxLength(1000);
        builder.Property(x => x.MovementState).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CustomerNote).HasMaxLength(2000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasIndex(x => x.InvoiceId).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.CreatedAt });
        builder.HasIndex(x => new { x.BranchId, x.Method, x.MovementState, x.CreatedAt });
        builder.HasIndex(x => new { x.BranchId, x.ServiceCodeSnapshot, x.CreatedAt });
        builder.HasIndex(x => new { x.BranchId, x.CustomerId, x.CreatedAt });
        builder.HasIndex(x => new { x.BranchId, x.DestinationCountryCode, x.DestinationCityOrState, x.CreatedAt });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.ReferredByClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerShipmentPriceComponentConfiguration
    : IEntityTypeConfiguration<CustomerShipmentPriceComponent>
{
    public void Configure(EntityTypeBuilder<CustomerShipmentPriceComponent> builder)
    {
        builder.ToTable("customer_shipment_price_components", table =>
        {
            table.HasCheckConstraint("CK_customer_shipment_price_quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_customer_shipment_price_unit_price", "\"UnitPrice\" >= 0");
            table.HasCheckConstraint("CK_customer_shipment_price_amount", "\"LineAmount\" >= 0");
            table.HasCheckConstraint("CK_customer_shipment_price_sort", "\"SortOrder\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CodeSnapshot).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DescriptionSnapshot).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.UnitSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineAmount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.CustomerShipmentId, x.SortOrder }).IsUnique();
        builder.HasIndex(x => x.CataloguePriceId);
        builder.HasOne<CustomerShipment>().WithMany().HasForeignKey(x => x.CustomerShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Operation).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ResourceType).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.ActorUserId, x.Operation, x.Key }).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
