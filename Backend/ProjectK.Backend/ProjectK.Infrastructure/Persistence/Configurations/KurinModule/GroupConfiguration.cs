using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.HasKey(e => e.GroupKey);
        builder.Property(e => e.Description)
            .HasMaxLength(1000);
        builder.Property(e => e.SilhouetteBlobName)
            .HasMaxLength(500);
        builder.Property(e => e.ProbeEdition)
            .HasMaxLength(64);
        builder.HasOne(e => e.Kurin)
            .WithMany(k => k.Groups)
            .HasForeignKey(e => e.KurinKey)
            .OnDelete(DeleteBehavior.Cascade);

        // A dissolved гурток is gone everywhere but the archive, which reads past this filter.
        builder.HasQueryFilter(e => e.DissolvedAtUtc == null);
    }
}
