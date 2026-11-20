using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>Held by key with no foreign key, as every score table: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreItemConfiguration : IEntityTypeConfiguration<ScoreItem>
{
    public void Configure(EntityTypeBuilder<ScoreItem> builder)
    {
        builder.HasKey(e => e.ScoreItemKey);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.KurinKey);
    }
}
