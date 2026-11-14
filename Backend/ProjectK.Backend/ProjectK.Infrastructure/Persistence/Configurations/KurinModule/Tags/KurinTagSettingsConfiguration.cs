using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Tags;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule.Tags;

internal sealed class KurinTagSettingsConfiguration : IEntityTypeConfiguration<KurinTagSettings>
{
    public void Configure(EntityTypeBuilder<KurinTagSettings> builder)
    {
        builder.HasKey(e => e.KurinKey);
        builder.HasOne<Kurin>()
            .WithOne()
            .HasForeignKey<KurinTagSettings>(e => e.KurinKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
