using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Rules.IdentityModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;
using KurinEntity = ProjectK.Common.Entities.KurinModule.Kurin;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

internal static class KurinMapping
{
    /// <summary>
    /// The kurin with how many people belong to it now; the count is the membership's answer
    /// (<c>IMembershipDirectory.CountCurrentInKurinAsync</c>), never the kurin's own navigation.
    /// </summary>
    public static KurinDto ToDto(this KurinEntity kurin, IBlobReadLinks links, int currentUserCount) => new(
        KurinKey: kurin.KurinKey,
        Number: kurin.Number,
        Branch: kurin.Branch,
        Branches: kurin.Numbering,
        KraiCode: kurin.KraiCode,
        Stanytsia: kurin.Stanytsia,
        RegionOrCountry: kurin.RegionOrCountry,
        NamedAfter: kurin.NamedAfter,
        Description: kurin.Description,
        CurrentUserCount: currentUserCount,
        ProfileVerificationEnabled: kurin.ProfileVerificationEnabled,
        ScoreEnabled: kurin.IsScoreEnabled,
        RoleNames: KurinRoleNames.Read(kurin.RoleNames).ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
        EmblemUrl: links.For(kurin.EmblemBlobName),
        CanRenameRoles: KurinRoleNames.IsAvailableFor(kurin.Branch));

    /// <summary>What the kurin lets its people under 18 share. A kurin that never set it lets them share nothing.</summary>
    public static KurinCardSharingDto ToDto(this KurinCardSharing? settings) =>
        settings is null
            ? new KurinCardSharingDto(false, MemberCardShare.DefaultFields, false)
            : new KurinCardSharingDto(settings.MinorsMayShare, settings.MinorFields, settings.MinorsMayLink);

    /// <summary>Every capability with each audience's reach, its default and whether the cell is locked.</summary>
    public static KurinAccessPolicyDto ToDto(this KurinAccessPolicy policy)
    {
        var rows = KurinCapabilityRules.All
            .Select(descriptor => new KurinCapabilityDto(
                descriptor.Key,
                descriptor.Group,
                descriptor.YouthConfigurable,
                descriptor.Reaches,
                KurinCapabilityRules.Audiences
                    .Select(audience => new AccessPolicyCellDto(
                        audience,
                        policy.Reach(descriptor.Key, audience),
                        descriptor.Defaults[audience],
                        descriptor.IsLocked(audience)))
                    .ToList()))
            .ToList();

        return new KurinAccessPolicyDto(KurinCapabilityRules.Audiences, rows);
    }

    /// <summary>
    /// Writes the kurin's own record. The key, the край, the гілки, the role names and the emblem
    /// are never written here: each has its own request and its own checks.
    /// </summary>
    public static void ApplyTo(this UpsertKurinCommand request, KurinEntity kurin, DateTime now)
    {
        kurin.SetNumber(request.Number);
        kurin.Stanytsia = TextRules.TrimOrNull(request.Stanytsia);
        kurin.RegionOrCountry = TextRules.TrimOrNull(request.RegionOrCountry);
        kurin.NamedAfter = TextRules.TrimOrNull(request.NamedAfter);
        kurin.Description = TextRules.TrimOrNull(request.Description);
        kurin.ProfileVerificationEnabled = request.ProfileVerificationEnabled;
        if (request.ScoreEnabled is bool scoreEnabled)
        {
            kurin.ScoreEnabled = scoreEnabled;
        }

        kurin.UpdatedDate = now;
    }
}
