using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

internal static class ThirdProbeMapping
{
    public static readonly ProbeProjectLimitsDto Limits = new(
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

    public static ProbeProjectDto ToDto(this ProbeProject project, IBlobReadLinks links) => new(
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
            .Select(image => image.ToDto(links))
            .ToList());

    public static ProbeProjectImageDto ToDto(this ProbeProjectImage image, IBlobReadLinks links) =>
        new(image.ProbeProjectImageKey, links.For(image.BlobName)!);
}
