using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class MemberAwardConfiguration : IEntityTypeConfiguration<MemberAward>
{
    public void Configure(EntityTypeBuilder<MemberAward> builder)
    {
        builder.HasKey(e => e.MemberAwardKey);
        builder.Property(e => e.Level)
            .HasConversion<int?>();
        builder.Property(e => e.CatalogId).HasMaxLength(MemberAward.CatalogIdMaxLength);
        builder.Property(e => e.Status)
            .HasConversion<int>();
        builder.HasIndex(e => new { e.MemberKey, e.Level });
        builder.HasOne(e => e.Member)
            .WithMany(m => m.MemberAwards)
            .HasForeignKey(e => e.MemberKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
