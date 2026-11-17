using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>
/// The КВ's private score: its own two tables, so nothing of it can leak into a public read by
/// a missed filter — the public ledger simply never loads them.
/// </summary>
internal sealed class PrivateScoreCriterionConfiguration : IEntityTypeConfiguration<PrivateScoreCriterion>
{
    public void Configure(EntityTypeBuilder<PrivateScoreCriterion> builder)
    {
        builder.HasKey(e => e.PrivateScoreCriterionKey);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.KurinKey);
    }
}
