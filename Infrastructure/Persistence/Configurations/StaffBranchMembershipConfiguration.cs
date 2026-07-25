using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class StaffBranchMembershipConfiguration : IEntityTypeConfiguration<StaffBranchMembership>
{
    public void Configure(EntityTypeBuilder<StaffBranchMembership> builder)
    {
        builder.ToTable("staff_branch_memberships");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserAccountId, x.BranchId }).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.UserAccountId });
        builder.HasIndex(x => new { x.UserAccountId, x.IsPrimary })
            .IsUnique().HasFilter("\"IsPrimary\" = TRUE");
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
