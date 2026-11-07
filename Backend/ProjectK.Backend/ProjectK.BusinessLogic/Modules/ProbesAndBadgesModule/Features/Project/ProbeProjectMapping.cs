using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

internal static class ProbeProjectMapping
{
    public static readonly ProbeProjectLimitsResponse Limits = new(
        ProbeProjectRules.MaxMainProjects,
        ProbeProjectRules.MaxSpareProjects,
        ProbeProjectRules.MaxSharedMainProjects,
        ProbeProjectRules.MaxImagesPerProject,
        ProbeProjectRules.MaxImageBytes,
        ProbeProjectRules.MaxCertificateBytes,
        ProbeProjectRules.MaxStoredBytes,
        ProbeProjectRules.MaxTextLength,
        ProbeProjectRules.RequiredBadges,
        ProbeProjectRules.YouthEndsAtAge);

    private static readonly ProbeProjectReach[] Reaches =
        Enum.GetValues<ProbeProjectReach>().Where(reach => reach != ProbeProjectReach.None).ToArray();

    public static IReadOnlyList<ProbeProjectReach> Split(ProbeProjectReach reach) =>
        Reaches.Where(flag => reach.HasFlag(flag)).ToList();

    public static ProbeProjectReach Join(IEnumerable<ProbeProjectReach>? reaches) =>
        (reaches ?? []).Aggregate(ProbeProjectReach.None, (all, flag) => all | flag);

    public static ProbeProjectResponse ToResponse(ProbeProject project, IBlobReadLinks links) => new(
        project.ProbeProjectKey,
        project.Title,
        project.Area,
        Split(project.Reach),
        project.IsShared,
        project.SharedWith,
        project.IsSpare,
        project.StartsOn,
        project.EndsOn,
        project.Location,
        project.EventLeads,
        project.CoordinatorName,
        project.CoordinatorContact,
        project.Description,
        project.Goal,
        project.Plan,
        project.Results,
        project.ApprovedAtUtc,
        project.ApprovedByName,
        project.CompletedAtUtc,
        project.CompletedByName,
        project.CoordinatorComment,
        project.UpdatedDate,
        project.UpdatedByName,
        project.Images
            .OrderBy(image => image.CreatedDate)
            .Select(image => ToResponse(image, links))
            .ToList());

    public static ProbeProjectImageResponse ToResponse(ProbeProjectImage image, IBlobReadLinks links) =>
        new(image.ProbeProjectImageKey, links.For(image.BlobName)!);
}

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
