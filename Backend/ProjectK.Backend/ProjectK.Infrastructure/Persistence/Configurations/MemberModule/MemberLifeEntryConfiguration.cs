using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class MemberLifeEntryConfiguration : IEntityTypeConfiguration<MemberLifeEntry>
{
    public void Configure(EntityTypeBuilder<MemberLifeEntry> builder)
    {
        builder.HasKey(e => e.MemberLifeEntryKey);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Course).HasConversion<int?>();
        builder.Property(e => e.Title).HasMaxLength(MemberLifeEntry.TitleMaxLength);
        builder.Property(e => e.Place).HasMaxLength(MemberLifeEntry.PlaceMaxLength);
        builder.Property(e => e.Commandant).HasMaxLength(MemberLifeEntry.PersonMaxLength);
        builder.Property(e => e.Role).HasMaxLength(MemberLifeEntry.RoleMaxLength);
        builder.HasIndex(e => e.MemberKey);
        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
