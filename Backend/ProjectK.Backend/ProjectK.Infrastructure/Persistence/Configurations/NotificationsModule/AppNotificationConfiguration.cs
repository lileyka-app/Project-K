using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.NotificationsModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.NotificationsModule;

internal sealed class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        builder.HasKey(e => e.NotificationKey);
        builder.Property(e => e.Type)
            .HasConversion<int>();
        builder.Property(e => e.Severity)
            .HasConversion<int>();
        builder.Property(e => e.Title)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(e => e.Body)
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(e => e.EntityType)
            .HasMaxLength(100);
        builder.Property(e => e.Route)
            .HasMaxLength(1000);
        builder.Property(e => e.PayloadJson)
            .HasMaxLength(2000);
        builder.Property(e => e.DeduplicationKey)
            .HasMaxLength(300);
        builder.HasIndex(e => new { e.RecipientUserKey, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.RecipientUserKey, e.ReadAtUtc });
        builder.HasIndex(e => new { e.RecipientUserKey, e.DeduplicationKey })
            .HasFilter("[DeduplicationKey] IS NOT NULL AND [ReadAtUtc] IS NULL");
    }
}
