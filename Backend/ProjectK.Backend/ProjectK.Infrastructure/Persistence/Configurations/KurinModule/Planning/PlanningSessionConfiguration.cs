using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule.Planning;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule.Planning;

internal sealed class PlanningSessionConfiguration : IEntityTypeConfiguration<PlanningSession>
{
    public void Configure(EntityTypeBuilder<PlanningSession> builder)
    {
        builder.HasKey(e => e.PlanningSessionKey);
        builder.HasOne(e => e.Kurin)
            .WithMany(k => k.PlanningSessions)
            .HasForeignKey(e => e.KurinKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
