using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ScoreModule;

/// <summary>Held by key with no foreign key, as every score table: see <see cref="KurinScoreSettingsConfiguration"/>.</summary>
internal sealed class ScoreAttendanceRateConfiguration : IEntityTypeConfiguration<ScoreAttendanceRate>
{
    public void Configure(EntityTypeBuilder<ScoreAttendanceRate> builder)
    {
        builder.HasKey(e => e.ScoreAttendanceRateKey);
        builder.HasIndex(e => e.KurinKey);
        builder.HasIndex(e => e.AgendaCategoryKey).IsUnique().HasFilter("[AgendaCategoryKey] IS NOT NULL");
        builder.HasIndex(e => e.AgendaItemKey).IsUnique().HasFilter("[AgendaItemKey] IS NOT NULL");
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ScoreAttendanceRates_OneTarget",
            "([AgendaCategoryKey] IS NULL AND [AgendaItemKey] IS NOT NULL) OR ([AgendaCategoryKey] IS NOT NULL AND [AgendaItemKey] IS NULL)"));
    }
}
