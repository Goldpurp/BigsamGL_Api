using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class CustomerLoyaltyAccountConfiguration : IEntityTypeConfiguration<CustomerLoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<CustomerLoyaltyAccount> builder)
    {
        builder.ToTable("customer_loyalty_accounts", table =>
        {
            table.HasCheckConstraint("CK_loyalty_account_available_points", "\"AvailablePoints\" >= 0");
            table.HasCheckConstraint("CK_loyalty_account_lifetime_points", "\"LifetimeEarnedPoints\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CustomerId).IsUnique();
        builder.HasIndex(x => new { x.LifetimeEarnedPoints, x.CustomerId });
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LoyaltyPointTransactionConfiguration : IEntityTypeConfiguration<LoyaltyPointTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyPointTransaction> builder)
    {
        builder.ToTable("loyalty_point_transactions", table =>
        {
            table.HasCheckConstraint("CK_loyalty_transaction_points", "\"Points\" <> 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.CustomerId, x.OccurredAt, x.Id });
        builder.HasIndex(x => x.ReferredCustomerId);
        builder.HasIndex(x => x.InvoiceId).IsUnique().HasFilter("\"InvoiceId\" IS NOT NULL");
        builder.HasOne<CustomerLoyaltyAccount>().WithMany().HasForeignKey(x => x.CustomerLoyaltyAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.ReferredCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
