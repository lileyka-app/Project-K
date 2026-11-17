using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.InfrastructureModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.InfrastructureModule;

/// <summary>
/// A device's push endpoint. Unique, so a device that signs in as someone else moves its row;
/// the account's rows go with the account.
/// </summary>
internal sealed class WebPushSubscriptionConfiguration : IEntityTypeConfiguration<WebPushSubscription>
{
    public void Configure(EntityTypeBuilder<WebPushSubscription> builder)
    {
        builder.HasKey(e => e.WebPushSubscriptionKey);
        builder.Property(e => e.Endpoint)
            .HasMaxLength(800)
            .IsRequired();
        builder.Property(e => e.P256dh)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(e => e.Auth)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(e => e.UserAgent)
            .HasMaxLength(300);
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(e => e.UserKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.Endpoint).IsUnique();
    }
}
