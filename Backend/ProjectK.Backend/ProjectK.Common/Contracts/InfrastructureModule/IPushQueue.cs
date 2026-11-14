using ProjectK.Common.Dtos.InfrastructureModule;

namespace ProjectK.Common.Contracts.InfrastructureModule;

/// <summary>
/// Hands a saved in-app notification over to be sent to the recipient's phones as well. Never
/// blocks and never throws: the push leaves after the request, and a full queue drops the oldest
/// rather than hold up whoever raised the notification.
/// </summary>
public interface IPushQueue
{
    void Enqueue(AppNotificationDto notification);
}
