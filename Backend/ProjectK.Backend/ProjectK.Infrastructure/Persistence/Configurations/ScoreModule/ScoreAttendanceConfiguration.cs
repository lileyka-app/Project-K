using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>One of the two «only once» rules of точкування: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreAttendanceConfiguration : IEntityTypeConfiguration<ScoreAttendance>
{
    public void Configure(EntityTypeBuilder<ScoreAttendance> builder)
    {
        builder.HasKey(e => e.ScoreAttendanceKey);
        builder.Ignore(e => e.IsRemoved);
        builder.HasIndex(e => new { e.MembershipKey, e.AgendaItemKey, e.OccurrenceStartUtc })
            .IsUnique()
            .HasFilter("[RemovedAtUtc] IS NULL")
            .HasDatabaseName("IX_ScoreAttendances_OnePerOccurrence");
        builder.HasIndex(e => new { e.KurinKey, e.AgendaItemKey });
    }
}
