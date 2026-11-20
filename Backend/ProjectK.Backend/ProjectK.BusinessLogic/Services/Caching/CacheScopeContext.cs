using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Services.Caching;

public sealed record CacheScopeContext(
    Guid? UserId,
    Guid? KurinKey,
    IReadOnlyCollection<string> Roles)
{
    public static CacheScopeContext From(ICurrentUserContext currentUserContext)
    {
        return new CacheScopeContext(
            currentUserContext.UserId,
            currentUserContext.KurinKey,
            currentUserContext.Roles);
    }
}
