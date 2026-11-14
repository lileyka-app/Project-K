using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ProbesAndBadgesModule;

internal sealed class ProbeProgressConfiguration : IEntityTypeConfiguration<ProbeProgress>
{
    public void Configure(EntityTypeBuilder<ProbeProgress> builder)
    {
        builder.HasKey(e => e.ProbeProgressKey);
        builder.Property(e => e.ProbeId)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(e => e.Status)
            .HasConversion<int>();
        builder.HasIndex(e => new { e.MemberKey, e.ProbeId })
            .IsUnique();
    }
}
