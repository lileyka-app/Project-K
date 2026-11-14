using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>Held by key with no foreign key, as every score table: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreStageConfiguration : IEntityTypeConfiguration<ScoreStage>
{
    public void Configure(EntityTypeBuilder<ScoreStage> builder)
    {
        builder.HasKey(e => e.ScoreStageKey);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.KurinKey);
    }
}
