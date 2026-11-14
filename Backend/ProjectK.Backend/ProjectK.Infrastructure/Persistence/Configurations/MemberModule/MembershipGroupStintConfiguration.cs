using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class MembershipGroupStintConfiguration : IEntityTypeConfiguration<MembershipGroupStint>
{
    public void Configure(EntityTypeBuilder<MembershipGroupStint> builder)
    {
        builder.HasKey(e => e.MembershipGroupStintKey);
        builder.HasOne(e => e.Membership)
            .WithMany()
            .HasForeignKey(e => e.MembershipKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.MembershipKey, e.ToUtc });
        builder.HasIndex(e => e.GroupKey);
    }
}
