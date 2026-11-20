using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ProbesAndBadgesModule;

internal sealed class ProbePointProgressConfiguration : IEntityTypeConfiguration<ProbePointProgress>
{
    public void Configure(EntityTypeBuilder<ProbePointProgress> builder)
    {
        builder.HasKey(e => e.ProbePointProgressKey);
        builder.Property(e => e.ProbeId)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(e => e.PointId)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(e => e.SignedByRole)
            .HasMaxLength(50);
        builder.HasIndex(e => new { e.MemberKey, e.ProbeId, e.PointId })
            .IsUnique();
    }
}
