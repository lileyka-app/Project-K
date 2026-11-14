using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>
/// Who stands in the КВ's own book — the Звʼязковий and the впорядники — and that this is their kurin.
/// The endpoint lets in only those with KurinScorePrivate; here it is checked once more against the
/// kurin the caller acts in, as every score handler does.
/// </summary>
public sealed class PrivateScoreAccess
{
    private readonly ScoreAccess _access;
    private readonly IResourceAccessService _resourceAccess;

    public PrivateScoreAccess(ScoreAccess access, IResourceAccessService resourceAccess)
    {
        _access = access;
        _resourceAccess = resourceAccess;
    }

    public async Task<ServiceResult<T>?> RefuseAsync<T>(Guid kurinKey, ResourceAction action, CancellationToken cancellationToken)
    {
        if (_access.Refuse<T>(kurinKey) is { } refused)
        {
            return refused;
        }

        var decision = await _resourceAccess.CheckAccessAsync(ResourceType.KurinScorePrivate, action, kurinKey, cancellationToken);
        return decision.IsAllowed ? null : ScoreErrors.Refused<T>();
    }
}
