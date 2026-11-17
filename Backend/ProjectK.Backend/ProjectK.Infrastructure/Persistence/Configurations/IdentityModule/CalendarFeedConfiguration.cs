using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.IdentityModule;

internal sealed class CalendarFeedConfiguration : IEntityTypeConfiguration<CalendarFeed>
{
    public void Configure(EntityTypeBuilder<CalendarFeed> builder)
    {
        builder.HasKey(e => e.CalendarFeedKey);
        builder.Property(e => e.TokenHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();
        builder.Property(e => e.RevokedReason).HasConversion<int>();
        // Looked up by hash on every fetch, and swept per user by a security change.
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.RevokedAtUtc });
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
