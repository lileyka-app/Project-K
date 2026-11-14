using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>Held by key with no foreign key, as every score table: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreTrailEventConfiguration : IEntityTypeConfiguration<ScoreTrailEvent>
{
    public void Configure(EntityTypeBuilder<ScoreTrailEvent> builder)
    {
        builder.HasKey(e => e.ScoreTrailEventKey);
        builder.Property(e => e.Subject).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.SubjectKey);
        builder.HasIndex(e => e.KurinKey);
    }
}
