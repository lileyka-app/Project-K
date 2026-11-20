using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>Held by key with no foreign key, as every score table: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreNotHeldOccurrenceConfiguration : IEntityTypeConfiguration<ScoreNotHeldOccurrence>
{
    public void Configure(EntityTypeBuilder<ScoreNotHeldOccurrence> builder)
    {
        builder.HasKey(e => e.ScoreNotHeldOccurrenceKey);
        builder.HasIndex(e => new { e.AgendaItemKey, e.OccurrenceStartUtc })
            .IsUnique()
            .HasDatabaseName("IX_ScoreNotHeldOccurrences_OnePerOccurrence");
        builder.HasIndex(e => e.KurinKey);
    }
}
