using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> b)
    {
        b.ToTable("guests"); b.HasKey(x => x.Id); b.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254); b.Property(x => x.Phone).HasMaxLength(40); b.Property(x => x.CompanyName).HasMaxLength(160);
        b.Property(x => x.CountryCode).HasMaxLength(5); b.Property(x => x.Source).HasMaxLength(80); b.Property(x => x.ServiceInterest).HasMaxLength(160);
        b.Property(x => x.Notes).HasMaxLength(2000); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.Email, x.Status }); b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.ConvertedCustomerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> b)
    {
        b.ToTable("client_profiles"); b.HasKey(x => x.Id); b.HasIndex(x => x.CustomerId).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30); b.Property(x => x.TaxNumber).HasMaxLength(80);
        b.Property(x => x.BusinessRegistrationNumber).HasMaxLength(100); b.Property(x => x.BillingCurrency).HasMaxLength(3);
        b.Property(x => x.CreditLimit).HasPrecision(18, 2); b.Property(x => x.InternalNotes).HasMaxLength(4000); b.Property(x => x.RiskNotes).HasMaxLength(2000);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AccountManagerUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.PreferredBranchId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> b)
    {
        b.ToTable("company_profiles"); b.HasKey(x => x.Id); b.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
        b.Property(x => x.TradingName).HasMaxLength(200).IsRequired(); b.Property(x => x.RegistrationNumber).HasMaxLength(100);
        b.Property(x => x.TaxNumber).HasMaxLength(100); b.Property(x => x.Website).HasMaxLength(250); b.Property(x => x.Email).HasMaxLength(254);
        b.Property(x => x.Phone).HasMaxLength(40); b.Property(x => x.RegisteredAddress).HasMaxLength(500); b.Property(x => x.BaseCurrency).HasMaxLength(3);
        b.Property(x => x.TimeZone).HasMaxLength(80); b.Property(x => x.LogoStorageKey).HasMaxLength(500);
    }
}

public class BranchControlConfiguration : IEntityTypeConfiguration<BranchControl>
{
    public void Configure(EntityTypeBuilder<BranchControl> b)
    {
        b.ToTable("branch_controls"); b.HasKey(x => x.Id); b.HasIndex(x => x.BranchId).IsUnique();
        b.Property(x => x.CapacityAlertPercent).HasPrecision(5, 2); b.Property(x => x.EmergencyNotice).HasMaxLength(1000);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.LastControlledByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CommunicationMessageConfiguration : IEntityTypeConfiguration<CommunicationMessage>
{
    public void Configure(EntityTypeBuilder<CommunicationMessage> b)
    {
        b.ToTable("communication_messages"); b.HasKey(x => x.Id); b.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.RecipientEmail).HasMaxLength(254).IsRequired(); b.Property(x => x.Subject).HasMaxLength(250).IsRequired();
        b.Property(x => x.Body).HasMaxLength(10000).IsRequired(); b.Property(x => x.ReferenceType).HasMaxLength(80);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30); b.Property(x => x.Provider).HasMaxLength(80); b.Property(x => x.ProviderReference).HasMaxLength(150);
        b.HasIndex(x => new { x.CustomerId, x.CreatedAt }); b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CommunicationAttachmentConfiguration : IEntityTypeConfiguration<CommunicationAttachment>
{
    public void Configure(EntityTypeBuilder<CommunicationAttachment> b)
    {
        b.ToTable("communication_attachments"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.CommunicationMessageId, x.ShipmentDocumentId }).IsUnique();
        b.HasOne<CommunicationMessage>().WithMany().HasForeignKey(x => x.CommunicationMessageId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ShipmentDocument>().WithMany().HasForeignKey(x => x.ShipmentDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
