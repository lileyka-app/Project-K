using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>One of the two «only once» rules of точкування: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreEntryConfiguration : IEntityTypeConfiguration<ScoreEntry>
{
    public void Configure(EntityTypeBuilder<ScoreEntry> builder)
    {
        builder.HasKey(e => e.ScoreEntryKey);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Ignore(e => e.IsDeleted);
        builder.HasIndex(e => new { e.MembershipKey, e.AgendaItemKey, e.OccurrenceStartUtc, e.ScoreItemKey })
            .IsUnique()
            .HasFilter("[MembershipKey] IS NOT NULL AND [AgendaItemKey] IS NOT NULL AND [ScoreItemKey] IS NOT NULL AND [DeletedAtUtc] IS NULL")
            .HasDatabaseName("IX_ScoreEntries_ItemOncePerPerson");
        builder.HasIndex(e => new { e.GroupKey, e.AgendaItemKey, e.OccurrenceStartUtc, e.ScoreItemKey })
            .IsUnique()
            .HasFilter("[GroupKey] IS NOT NULL AND [AgendaItemKey] IS NOT NULL AND [ScoreItemKey] IS NOT NULL AND [DeletedAtUtc] IS NULL")
            .HasDatabaseName("IX_ScoreEntries_ItemOncePerGroup");
        builder.HasIndex(e => e.KurinKey);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ScoreEntries_OneTarget",
            "([MembershipKey] IS NULL AND [GroupKey] IS NOT NULL) OR ([MembershipKey] IS NOT NULL AND [GroupKey] IS NULL)"));
    }
}
