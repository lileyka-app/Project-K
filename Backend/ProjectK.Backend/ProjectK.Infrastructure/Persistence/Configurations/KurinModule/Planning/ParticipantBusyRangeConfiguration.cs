using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule.Planning;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule.Planning;

internal sealed class ParticipantBusyRangeConfiguration : IEntityTypeConfiguration<ParticipantBusyRange>
{
    public void Configure(EntityTypeBuilder<ParticipantBusyRange> builder)
    {
        builder.HasKey(e => e.ParticipantBusyRangeKey);
        builder.HasOne(e => e.PlanningParticipant)
            .WithMany(pp => pp.BusyRanges)
            .HasForeignKey(e => e.PlanningParticipantKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
