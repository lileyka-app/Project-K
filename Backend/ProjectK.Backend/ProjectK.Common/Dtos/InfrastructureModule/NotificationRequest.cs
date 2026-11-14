using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.InfrastructureModule;

public sealed class NotificationRequest
{
    public Guid RecipientUserKey { get; set; }
    public AppNotificationType Type { get; set; }
    public AppNotificationSeverity Severity { get; set; } = AppNotificationSeverity.Info;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityKey { get; set; }
    public string? Route { get; set; }
    public string? PayloadJson { get; set; }
    public Guid? ActorUserKey { get; set; }
    public string? DeduplicationKey { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
