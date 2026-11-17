using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.HasKey(e => e.MembershipKey);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.HasOne<Kurin>()
            .WithMany()
            .HasForeignKey(e => e.KurinKey)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(e => e.GroupKey)
            .OnDelete(DeleteBehavior.NoAction);

        // The reads this table exists for: everyone in a kurin, every kurin of one person,
        // and the scope of the account making a request.
        builder.HasIndex(e => new { e.KurinKey, e.LeftAtUtc });
        builder.HasIndex(e => new { e.MemberKey, e.LeftAtUtc });
        builder.HasIndex(e => new { e.UserKey, e.LeftAtUtc });

        // A person belongs to a kurin once at a time. Past memberships are excluded, so
        // rejoining after leaving is allowed and being in it twice at once is not.
        builder.HasIndex(e => new { e.MemberKey, e.KurinKey })
            .IsUnique()
            .HasFilter("[LeftAtUtc] IS NULL");
    }
}
