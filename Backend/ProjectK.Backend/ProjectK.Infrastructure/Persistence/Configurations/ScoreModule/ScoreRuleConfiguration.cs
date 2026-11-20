using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>Held by key with no foreign key, as every score table: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreRuleConfiguration : IEntityTypeConfiguration<ScoreRule>
{
    public void Configure(EntityTypeBuilder<ScoreRule> builder)
    {
        builder.HasKey(e => e.ScoreRuleKey);
        builder.Property(e => e.Source).HasConversion<int>();
        builder.HasIndex(e => new { e.KurinKey, e.Source, e.Variant, e.FromDate }).IsUnique();
    }
}
