using ProjectK.Common.Enums.InfrastructureModule;

namespace ProjectK.Common.Dtos.InfrastructureModule;

public class AppNotificationDto
{
    public Guid NotificationKey { get; set; }
    public Guid RecipientUserKey { get; set; }
    public AppNotificationType Type { get; set; }
    public AppNotificationSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityKey { get; set; }
    public string? Route { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public Guid? ActorUserKey { get; set; }
    public string? DeduplicationKey { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsRead => ReadAtUtc.HasValue;
}
