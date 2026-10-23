using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>
/// The two questions every score write asks: is this the kurin the caller acts in, and may they score
/// this гурток. A youth in no гурток can be scored only by someone who scores the whole kurin, so the
/// check for them runs against the kurin itself.
/// </summary>
public sealed class ScoreAccess
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IResourceAccessService _resourceAccess;

    public ScoreAccess(ICurrentUserContext currentUser, IResourceAccessService resourceAccess)
    {
        _currentUser = currentUser;
        _resourceAccess = resourceAccess;
    }

    public ServiceResult<T>? Refuse<T>(Guid kurinKey) =>
        kurinKey == Guid.Empty || _currentUser.KurinKey != kurinKey ? new ServiceResult<T>(ResultType.Forbidden) : null;

    /// <summary>Whether the caller may do <paramref name="action"/> to the points of a гурток — or of the kurin's unplaced youths.</summary>
    public async Task<bool> MayScoreAsync(Guid kurinKey, Guid? groupKey, ResourceAction action, CancellationToken cancellationToken)
    {
        var decision = groupKey is { } group
            ? await _resourceAccess.CheckAccessAsync(ResourceType.GroupScore, action, group, cancellationToken)
            : await _resourceAccess.CheckAccessAsync(ResourceType.GroupScore, action, ResourceType.KurinScore, kurinKey, cancellationToken);
        return decision.IsAllowed;
    }

    /// <summary>
    /// Whether the caller may mark anyone at all here — the kurin's unplaced youths or one of
    /// <paramref name="groupKeys"/>. The same gate the sheet opens on.
    /// </summary>
    public async Task<bool> MayMarkAnyoneAsync(Guid kurinKey, IEnumerable<Guid> groupKeys, CancellationToken cancellationToken)
    {
        if (await MayScoreAsync(kurinKey, null, ResourceAction.Create, cancellationToken))
        {
            return true;
        }

        foreach (var groupKey in groupKeys)
        {
            if (await MayScoreAsync(kurinKey, groupKey, ResourceAction.Create, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> MayManageAsync(Guid kurinKey, CancellationToken cancellationToken) =>
        (await _resourceAccess.CheckAccessAsync(ResourceType.KurinScore, ResourceAction.Manage, kurinKey, cancellationToken)).IsAllowed;

    public static ServiceResult<T> Forbidden<T>() => new(ResultType.Forbidden);

    public static ServiceResult<T> NotFound<T>(string what) =>
        ServiceResult<T>.Failure(ResultType.NotFound, "ScoreNotFound", what);
}
