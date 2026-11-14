using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule.Planning;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule.Planning;

internal sealed class PlanningParticipantConfiguration : IEntityTypeConfiguration<PlanningParticipant>
{
    public void Configure(EntityTypeBuilder<PlanningParticipant> builder)
    {
        builder.HasKey(e => e.PlanningParticipantKey);
        builder.HasOne(e => e.PlanningSession)
            .WithMany(ps => ps.Participants)
            .HasForeignKey(e => e.PlanningSessionKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
