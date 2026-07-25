using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalizedFullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.JobTitle).HasMaxLength(120);
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(40).IsRequired();
        builder.Property(x => x.NormalizedPhone).HasMaxLength(40);
        builder.Property(x => x.CountryCode).HasMaxLength(5).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Tags).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => x.NormalizedFullName);
        builder.HasIndex(x => x.NormalizedEmail);
        builder.HasIndex(x => x.NormalizedPhone);
        builder.HasIndex(x => new { x.Type, x.Status, x.NextFollowUpAt });
        builder.HasIndex(x => new { x.OwnerUserId, x.Status, x.NextFollowUpAt });
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<SalesLead>().WithMany().HasForeignKey(x => x.SalesLeadId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.PreferredBranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ContactInteractionConfiguration : IEntityTypeConfiguration<ContactInteraction>
{
    public void Configure(EntityTypeBuilder<ContactInteraction> builder)
    {
        builder.ToTable("contact_interactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.ContactId, x.OccurredAt });
        builder.HasIndex(x => new { x.StaffUserId, x.NextActionAt });
        builder.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesLead>().WithMany().HasForeignKey(x => x.SalesLeadId).OnDelete(DeleteBehavior.SetNull);
    }
}
