namespace ProjectK.Common.Contracts.NotificationsModule;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? KurinKey { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}
