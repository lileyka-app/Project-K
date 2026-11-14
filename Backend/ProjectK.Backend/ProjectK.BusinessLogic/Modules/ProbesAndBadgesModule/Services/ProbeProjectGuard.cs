using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>What every write to the third probe checks first: the right probe, and the right person.</summary>
internal static class ProbeProjectGuard
{
    public static async Task<ServiceResult<T>?> RefuseAsync<T>(
        IProbeProjectAccess access,
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken,
        Func<IProbeProjectAccess, Task<bool>>? right = null)
    {
        if (!await access.IsProjectProbeAsync(memberKey, probeId, cancellationToken))
        {
            return ServiceResult<T>.Failure(
                ResultType.BadRequest, "NotProjectProbe", "Only the third probe of the person's line has projects.");
        }

        var allowed = right is null
            ? await access.CanEditAsync(memberKey, cancellationToken)
            : await right(access);
        return allowed ? null : new ServiceResult<T>(ResultType.Forbidden);
    }

    public static ServiceResult<T> Refuse<T>(string code, string message) =>
        ServiceResult<T>.Failure(ResultType.BadRequest, code, message);
}
