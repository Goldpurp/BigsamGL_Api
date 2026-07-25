using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ExchangeRateSnapshotConfiguration : IEntityTypeConfiguration<ExchangeRateSnapshot>
{
    public void Configure(EntityTypeBuilder<ExchangeRateSnapshot> b)
    {
        b.ToTable("exchange_rate_snapshots"); b.HasKey(x => x.Id);
        b.Property(x => x.BaseCurrency).HasMaxLength(3).IsRequired();
        b.Property(x => x.QuoteCurrency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Rate).HasPrecision(24, 10);
        b.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        b.Property(x => x.ProviderReference).HasMaxLength(500);
        b.HasIndex(x => new { x.BaseCurrency, x.QuoteCurrency, x.FetchedAt });
    }
}
