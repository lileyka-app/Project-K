using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ProbesAndBadgesModule;

internal sealed class ProbeProjectImageConfiguration : IEntityTypeConfiguration<ProbeProjectImage>
{
    public void Configure(EntityTypeBuilder<ProbeProjectImage> builder)
    {
        builder.HasKey(e => e.ProbeProjectImageKey);
        builder.Property(e => e.BlobName)
            .HasMaxLength(300)
            .IsRequired();
    }
}
