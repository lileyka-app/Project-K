using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.IdentityModule;

internal sealed class UserTileLayoutConfiguration : IEntityTypeConfiguration<UserTileLayout>
{
    public void Configure(EntityTypeBuilder<UserTileLayout> builder)
    {
        builder.HasKey(e => e.UserTileLayoutKey);
        builder.Property(e => e.BoardKey)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(e => e.TileOrderJson)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(e => e.HiddenTilesJson)
            .HasMaxLength(2000)
            .IsRequired()
            .HasDefaultValue("[]");
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.UserKey, e.BoardKey }).IsUnique();
    }
}
