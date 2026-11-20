using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class KurinCardSharingConfiguration : IEntityTypeConfiguration<KurinCardSharing>
{
    public void Configure(EntityTypeBuilder<KurinCardSharing> builder)
    {
        builder.HasKey(e => e.KurinCardSharingKey);
        builder.Property(e => e.MinorFields).HasConversion<int>();
        builder.HasIndex(e => e.KurinKey).IsUnique();
    }
}
