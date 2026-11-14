using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ProbesAndBadgesModule;

internal sealed class BadgeProgressConfiguration : IEntityTypeConfiguration<BadgeProgress>
{
    public void Configure(EntityTypeBuilder<BadgeProgress> builder)
    {
        builder.HasKey(e => e.BadgeProgressKey);
        builder.Property(e => e.BadgeId)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(e => e.Status)
            .HasConversion<int>();
        builder.HasIndex(e => new { e.MemberKey, e.BadgeId })
            .IsUnique();
    }
}
