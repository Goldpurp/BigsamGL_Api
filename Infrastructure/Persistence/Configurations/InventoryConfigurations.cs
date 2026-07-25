using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory_items", table =>
            table.HasCheckConstraint("CK_inventory_items_default_cost", "\"DefaultUnitCost\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.Property(x => x.Barcode).HasMaxLength(120);
        builder.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DefaultUnitCost).HasPrecision(18, 4);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(x => new { x.Type, x.IsActive, x.Name });
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.PreferredSupplierId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class InventoryLocationConfiguration : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> builder)
    {
        builder.ToTable("inventory_locations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.AddressOrArea).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.IsActive, x.Name });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryStockLevelConfiguration : IEntityTypeConfiguration<InventoryStockLevel>
{
    public void Configure(EntityTypeBuilder<InventoryStockLevel> builder)
    {
        builder.ToTable("inventory_stock_levels", table =>
        {
            table.HasCheckConstraint("CK_inventory_stock_on_hand", "\"QuantityOnHand\" >= 0");
            table.HasCheckConstraint("CK_inventory_stock_reserved", "\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"");
            table.HasCheckConstraint("CK_inventory_stock_cost", "\"AverageUnitCost\" >= 0");
            table.HasCheckConstraint("CK_inventory_stock_thresholds", "\"ReorderLevel\" >= 0 AND \"TargetStockLevel\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
        builder.Property(x => x.QuantityReserved).HasPrecision(18, 3);
        builder.Property(x => x.AverageUnitCost).HasPrecision(18, 4);
        builder.Property(x => x.ReorderLevel).HasPrecision(18, 3);
        builder.Property(x => x.TargetStockLevel).HasPrecision(18, 3);
        builder.Ignore(x => x.QuantityAvailable);
        builder.HasIndex(x => new { x.InventoryItemId, x.InventoryLocationId }).IsUnique();
        builder.HasIndex(x => new { x.InventoryLocationId, x.QuantityOnHand });
        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryLocation>().WithMany().HasForeignKey(x => x.InventoryLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("inventory_transactions", table =>
        {
            table.HasCheckConstraint("CK_inventory_transactions_quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_inventory_transactions_cost", "\"UnitCost\" >= 0 AND \"TotalCost\" >= 0");
            table.HasCheckConstraint("CK_inventory_transactions_location", "\"FromLocationId\" IS NOT NULL OR \"ToLocationId\" IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.TransactionNumber).IsUnique();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.TotalCost).HasPrecision(18, 2);
        builder.Property(x => x.ReferenceType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => new { x.InventoryItemId, x.OccurredAt });
        builder.HasIndex(x => new { x.FromLocationId, x.OccurredAt });
        builder.HasIndex(x => new { x.ToLocationId, x.OccurredAt });
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryLocation>().WithMany().HasForeignKey(x => x.FromLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryLocation>().WithMany().HasForeignKey(x => x.ToLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyPurchaseLine>().WithMany().HasForeignKey(x => x.CompanyPurchaseLineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
