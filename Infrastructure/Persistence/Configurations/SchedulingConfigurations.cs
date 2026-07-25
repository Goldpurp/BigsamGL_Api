using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", table =>
            table.HasCheckConstraint("CK_appointments_time", "\"EndsAt\" > \"StartsAt\""));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AppointmentNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.AppointmentNumber).IsUnique();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(300).IsRequired();
        builder.Property(x => x.MeetingUrl).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.StartsAt, x.Status });
        builder.HasIndex(x => new { x.OrganizerUserId, x.StartsAt, x.EndsAt });
        builder.HasIndex(x => new { x.AssignedUserId, x.StartsAt, x.EndsAt });
        builder.HasIndex(x => new { x.BranchId, x.StartsAt });
        builder.HasIndex(x => new { x.ReminderAt, x.ReminderSentAt });
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OrganizerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<SalesLead>().WithMany().HasForeignKey(x => x.SalesLeadId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class AppointmentAttendeeConfiguration : IEntityTypeConfiguration<AppointmentAttendee>
{
    public void Configure(EntityTypeBuilder<AppointmentAttendee> builder)
    {
        builder.ToTable("appointment_attendees");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.ResponseStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.AppointmentId, x.UserAccountId });
        builder.HasIndex(x => new { x.AppointmentId, x.ContactId });
        builder.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.SetNull);
    }
}
