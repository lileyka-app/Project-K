using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class LeadershipConfiguration : IEntityTypeConfiguration<Leadership>
{
    public void Configure(EntityTypeBuilder<Leadership> builder)
    {
        builder.HasKey(e => e.LeadershipKey);
        builder.Property(e => e.Type)
            .HasConversion<int>();
        builder.HasIndex(e => new { e.Type, e.KurinKey, e.GroupKey });
        builder.HasOne<Kurin>()
            .WithMany()
            .HasForeignKey(e => e.KurinKey)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Group>()
            .WithOne()
            .HasForeignKey<Leadership>(e => e.GroupKey)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
