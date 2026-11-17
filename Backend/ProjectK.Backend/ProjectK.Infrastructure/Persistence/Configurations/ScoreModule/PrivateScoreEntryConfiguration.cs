using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>The КВ's private score, kept apart from the public ledger: see <see cref="PrivateScoreCriterionConfiguration"/>.</summary>
internal sealed class PrivateScoreEntryConfiguration : IEntityTypeConfiguration<PrivateScoreEntry>
{
    public void Configure(EntityTypeBuilder<PrivateScoreEntry> builder)
    {
        builder.HasKey(e => e.PrivateScoreEntryKey);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Ignore(e => e.IsDeleted);
        builder.HasIndex(e => e.KurinKey);
        builder.HasIndex(e => e.MembershipKey);
    }
}
