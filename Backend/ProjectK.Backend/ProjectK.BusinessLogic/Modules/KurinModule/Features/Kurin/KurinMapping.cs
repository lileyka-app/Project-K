using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using KurinEntity = ProjectK.Common.Entities.KurinModule.Kurin;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

internal static class KurinMapping
{
    /// <summary>The kurin with the people it holds now, counted from its loaded memberships.</summary>
    public static KurinDto ToDto(this KurinEntity kurin, IBlobReadLinks links) =>
        kurin.ToDto(links, kurin.Memberships.Count(membership => membership.LeftAtUtc == null));

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

    /// <summary>
    /// Writes the kurin's own record. The key, the край, the гілки, the role names and the emblem
    /// are never written here: each has its own request and its own checks.
    /// </summary>
    public static void ApplyTo(this UpsertKurinCommand request, KurinEntity kurin, DateTime now)
    {
        kurin.SetNumber(request.Number);
        kurin.Stanytsia = Trimmed(request.Stanytsia);
        kurin.RegionOrCountry = Trimmed(request.RegionOrCountry);
        kurin.NamedAfter = Trimmed(request.NamedAfter);
        kurin.Description = Trimmed(request.Description);
        kurin.ProfileVerificationEnabled = request.ProfileVerificationEnabled;
        if (request.ScoreEnabled is bool scoreEnabled)
        {
            kurin.ScoreEnabled = scoreEnabled;
        }

        kurin.UpdatedDate = now;
    }

    /// <summary>What the kurin lets its people under 18 share. A kurin that never set it lets them share nothing.</summary>
    public static KurinCardSharingDto ToDto(this KurinCardSharing? settings) =>
        settings is null
            ? new KurinCardSharingDto(false, MemberCardShare.DefaultFields, false)
            : new KurinCardSharingDto(settings.MinorsMayShare, settings.MinorFields, settings.MinorsMayLink);

    /// <summary>Every capability with each audience's reach, its default and whether the cell is locked.</summary>
    public static KurinAccessPolicyDto ToDto(this KurinAccessPolicy policy)
    {
        var rows = KurinCapabilities.All
            .Select(descriptor => new KurinCapabilityDto(
                descriptor.Key,
                descriptor.Group,
                descriptor.YouthConfigurable,
                descriptor.Reaches,
                KurinCapabilities.Audiences
                    .Select(audience => new AccessPolicyCellDto(
                        audience,
                        policy.Reach(descriptor.Key, audience),
                        descriptor.Defaults[audience],
                        descriptor.IsLocked(audience)))
                    .ToList()))
            .ToList();

        return new KurinAccessPolicyDto(KurinCapabilities.Audiences, rows);
    }

    /// <summary>Free text as it is kept: trimmed, and blank as nothing.</summary>
    public static string? Trimmed(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
