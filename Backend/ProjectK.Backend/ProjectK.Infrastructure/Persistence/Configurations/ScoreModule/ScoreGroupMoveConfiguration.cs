using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>Held by key with no foreign key, as every score table: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreGroupMoveConfiguration : IEntityTypeConfiguration<ScoreGroupMove>
{
    public void Configure(EntityTypeBuilder<ScoreGroupMove> builder)
    {
        builder.HasKey(e => e.ScoreGroupMoveKey);
        builder.HasIndex(e => e.KurinKey);
        builder.HasIndex(e => e.MembershipKey);
    }
}
