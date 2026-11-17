using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.IdentityModule;

internal sealed class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.HasKey(e => e.UserRefreshTokenKey);
        builder.Property(e => e.Token)
            .HasMaxLength(512)
            .IsRequired();
        // Looked up by token on every refresh, and swept per user on a password change.
        builder.HasIndex(e => e.Token).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.RevokedAtUtc });
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
