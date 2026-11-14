using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule.Agenda;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule.Agenda;

internal sealed class AgendaAssignmentProgressConfiguration : IEntityTypeConfiguration<AgendaAssignmentProgress>
{
    public void Configure(EntityTypeBuilder<AgendaAssignmentProgress> builder)
    {
        builder.HasKey(e => e.AgendaAssignmentProgressKey);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasOne(e => e.Assignment)
            .WithMany(a => a.Progress)
            .HasForeignKey(e => e.AgendaAssignmentKey)
            .OnDelete(DeleteBehavior.Cascade);
        // One part per person per target; a second move overwrites the row.
        builder.HasIndex(e => new { e.AgendaAssignmentKey, e.MemberKey }).IsUnique();
    }
}
