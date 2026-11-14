using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.AgendaModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.AgendaModule;

internal sealed class AgendaResponseConfiguration : IEntityTypeConfiguration<AgendaResponse>
{
    public void Configure(EntityTypeBuilder<AgendaResponse> builder)
    {
        builder.HasKey(e => e.AgendaResponseKey);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasOne(e => e.AgendaItem)
            .WithMany(a => a.Responses)
            .HasForeignKey(e => e.AgendaItemKey)
            .OnDelete(DeleteBehavior.Cascade);
        // One answer per user per occurrence, NULL being the one-off event itself. SQL Server treats
        // NULLs as equal in a unique index, which is exactly the one-row-per-user rule a one-off
        // needs — but EF would add a «IS NOT NULL» filter by default and exempt those rows, so the
        // filter is switched off. The RSVP list also queries by item and occurrence.
        builder.HasIndex(e => new { e.AgendaItemKey, e.UserKey, e.OccurrenceStartUtc }).IsUnique().HasFilter(null);
    }
}
