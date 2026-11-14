using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>
/// Score module. Kurins, гуртки, memberships and events are held by key with no foreign key, as
/// in dues. The two "only once" rules of точкування are unique indexes over the rows still
/// standing, so they hold whoever writes and however fast.
/// </summary>
internal sealed class KurinScoreSettingsConfiguration : IEntityTypeConfiguration<KurinScoreSettings>
{
    public void Configure(EntityTypeBuilder<KurinScoreSettings> builder)
    {
        builder.HasKey(e => e.KurinScoreSettingsKey);
        builder.Property(e => e.Algorithm).HasConversion<int>();
        builder.HasIndex(e => e.KurinKey).IsUnique();
    }
}
