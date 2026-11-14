using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class MentorAssignmentConfiguration : IEntityTypeConfiguration<MentorAssignment>
{
    public void Configure(EntityTypeBuilder<MentorAssignment> builder)
    {
        builder.HasKey(e => e.MentorAssignmentKey);
        builder.HasOne(e => e.Group)
            .WithMany(g => g.MentorAssignments)
            .HasForeignKey(e => e.GroupKey)
            .OnDelete(DeleteBehavior.Cascade);
        // Follows the гурток's filter: a впорядник of a dissolved гурток leads nothing.
        builder.HasQueryFilter(e => e.Group.DissolvedAtUtc == null);
        // Revoking keeps the row as history, so the pair may repeat: one active assignment at a
        // time, any number of revoked ones. Unfiltered, re-assigning a mentor to a group they
        // had been removed from failed on this index with a 500.
        builder.HasIndex(e => new { e.MentorUserKey, e.GroupKey })
            .IsUnique()
            .HasFilter("[RevokedAtUtc] IS NULL");
        builder.HasIndex(e => e.MentorUserKey)
            .HasFilter("[RevokedAtUtc] IS NULL");
    }
}
