using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class LeadershipConfiguration : IEntityTypeConfiguration<Leadership>
{
    public void Configure(EntityTypeBuilder<Leadership> builder)
    {
        builder.HasKey(e => e.LeadershipKey);
        builder.Property(e => e.Type)
            .HasConversion<int>();
        builder.HasIndex(e => new { e.Type, e.KurinKey, e.GroupKey });
        builder.HasOne(e => e.Kurin)
            .WithMany(k => k.Leaderships)
            .HasForeignKey(e => e.KurinKey)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Group)
            .WithOne(g => g.Leadership)
            .HasForeignKey<Leadership>(e => e.GroupKey)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
