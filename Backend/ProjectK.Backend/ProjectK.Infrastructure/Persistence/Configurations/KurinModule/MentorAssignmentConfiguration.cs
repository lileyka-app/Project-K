using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class MentorAssignmentConfiguration : IEntityTypeConfiguration<MentorAssignment>
{
    public void Configure(EntityTypeBuilder<MentorAssignment> builder)
    {
        builder.HasKey(e => e.MentorAssignmentKey);
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(e => e.GroupKey)
            .OnDelete(DeleteBehavior.Cascade);
        // The query filter that follows the гурток's (a впорядник of a dissolved гурток leads nothing) needs
        // the context, so AppDbContext sets it.
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
