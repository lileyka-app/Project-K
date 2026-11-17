using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule.Tags;

internal sealed class MemberTagConfiguration : IEntityTypeConfiguration<MemberTag>
{
    public void Configure(EntityTypeBuilder<MemberTag> builder)
    {
        builder.HasKey(e => new { e.KurinTagKey, e.MemberKey });
        builder.HasOne(e => e.Tag)
            .WithMany(t => t.Members)
            .HasForeignKey(e => e.KurinTagKey)
            .OnDelete(DeleteBehavior.Cascade);
        // A deleted card takes its tags with it.
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(e => e.MemberKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.MemberKey);
    }
}
