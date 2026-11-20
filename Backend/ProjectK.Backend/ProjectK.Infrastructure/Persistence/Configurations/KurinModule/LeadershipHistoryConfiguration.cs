using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class LeadershipHistoryConfiguration : IEntityTypeConfiguration<LeadershipHistory>
{
    public void Configure(EntityTypeBuilder<LeadershipHistory> builder)
    {
        builder.HasKey(e => e.LeadershipHistoryKey);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(e => e.MemberKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Leadership)
            .WithMany(l => l.LeadershipHistories)
            .HasForeignKey(e => e.LeadershipKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.LeadershipKey, e.Role, e.StartDate });
        builder.HasIndex(e => new { e.MemberKey, e.StartDate });
    }
}
