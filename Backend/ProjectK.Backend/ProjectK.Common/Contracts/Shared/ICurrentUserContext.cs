namespace ProjectK.Common.Contracts.Shared;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? KurinKey { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}
