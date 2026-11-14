using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class MemberWarningConfiguration : IEntityTypeConfiguration<MemberWarning>
{
    public void Configure(EntityTypeBuilder<MemberWarning> builder)
    {
        builder.HasIndex(e => new { e.KurinKey, e.RevokedAtUtc });
        builder.HasKey(e => e.MemberWarningKey);
        builder.Property(e => e.Level)
            .HasConversion<int>();
        builder.HasIndex(e => new { e.MemberKey, e.Level });
        builder.HasIndex(e => e.ExpiresAtUtc);
        builder.HasOne(e => e.Member)
            .WithMany(m => m.MemberWarnings)
            .HasForeignKey(e => e.MemberKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
