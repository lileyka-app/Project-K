using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.AgendaModule;

internal sealed class AgendaItemConfiguration : IEntityTypeConfiguration<AgendaItem>
{
    public void Configure(EntityTypeBuilder<AgendaItem> builder)
    {
        builder.HasKey(e => e.AgendaItemKey);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.RecurrenceFrequency).HasConversion<int>();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Location).HasMaxLength(200);
        builder.HasOne<Kurin>()
            .WithMany(k => k.AgendaItems)
            .HasForeignKey(e => e.KurinKey)
            .OnDelete(DeleteBehavior.Cascade);
        // The calendar queries by kurin and date window, so index both.
        builder.HasIndex(e => new { e.KurinKey, e.StartUtc });
        // Every feed filters active vs archived, and the nightly sweep looks for old archived rows.
        builder.HasIndex(e => new { e.KurinKey, e.ArchivedAtUtc });
        // NoAction (not SetNull) so Kurin keeps a single cascade path to AgendaItems: Category→Kurin
        // is Cascade, and a second Kurin→Category→item(SetNull) path would trip SQL Server 1785.
        // DeleteAgendaCategoryCommand nulls out referencing items itself before removing the group.
        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.AgendaCategoryKey)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
