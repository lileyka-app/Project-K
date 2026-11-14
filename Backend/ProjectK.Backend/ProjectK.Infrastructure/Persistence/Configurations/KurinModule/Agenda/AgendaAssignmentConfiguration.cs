using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule.Agenda;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule.Agenda;

internal sealed class AgendaAssignmentConfiguration : IEntityTypeConfiguration<AgendaAssignment>
{
    public void Configure(EntityTypeBuilder<AgendaAssignment> builder)
    {
        builder.HasKey(e => e.AgendaAssignmentKey);
        builder.Property(e => e.TargetType).HasConversion<int>();
        builder.HasOne(e => e.AgendaItem)
            .WithMany(a => a.Assignments)
            .HasForeignKey(e => e.AgendaItemKey)
            .OnDelete(DeleteBehavior.Cascade);
        // One target appears once per item; also the lookup path for "what is assigned to me".
        builder.Property(e => e.CompletionMode).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => new { e.AgendaItemKey, e.TargetType, e.TargetKey }).IsUnique();
        builder.HasIndex(e => new { e.TargetType, e.TargetKey });
    }
}
