using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>What every write to the third probe checks first: the right probe, and the right person.</summary>
internal static class ProbeProjectGuard
{
    public static async Task<ServiceResult<T>?> RefuseAsync<T>(
        IProbesAndBadgesAccess access,
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken,
        Func<IProbesAndBadgesAccess, Task<bool>>? right = null)
    {
        if (!await access.IsProjectProbeAsync(memberKey, probeId, cancellationToken))
        {
            return ServiceResult<T>.Failure(
                ResultType.BadRequest, ProbesAndBadgesErrors.NotProjectProbe, "Only the third probe of the person's line has projects.");
        }

        var allowed = right is null
            ? await access.CanEditAsync(memberKey, cancellationToken)
            : await right(access);
        return allowed ? null : ServiceResult<T>.Failure(ResultType.Forbidden, ProbesAndBadgesErrors.ProbeProjectEditNotAllowed, "The third probe of this person is not yours to change.");
    }

    public static ServiceResult<T> Refuse<T>(string code, string message) =>
        ServiceResult<T>.Failure(ResultType.BadRequest, code, message);
}
