using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Authorization.KurinPolicy;

/// <summary>The one place a role is folded into the columns of a kurin's policy.</summary>
public static class PolicyAudiences
{
    /// <summary>
    /// The audiences <paramref name="roles"/> belong to. Every role carries the
    /// <see cref="PolicyAudience.Youth"/> baseline. The admin counts as the Звʼязковий: he passes
    /// every check anyway, and this keeps his reach the widest wherever reach is asked.
    /// </summary>
    public static IReadOnlySet<PolicyAudience> For(IEnumerable<string> roles)
    {
        var audiences = new HashSet<PolicyAudience>();
        foreach (var role in roles)
        {
            audiences.Add(PolicyAudience.Youth);
            if (Of(role) is { } audience)
            {
                audiences.Add(audience);
            }
        }

        return audiences;
    }

    private static PolicyAudience? Of(string role)
    {
        if (role.Equals(SystemRole.Admin, StringComparison.OrdinalIgnoreCase)
            || role == SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi))
        {
            return PolicyAudience.Zvyazkovyi;
        }

        if (role == SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk))
        {
            return PolicyAudience.Vykhovnyk;
        }

        if (role == SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Instruktor))
        {
            return PolicyAudience.Instruktor;
        }

        if (role == SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy))
        {
            return PolicyAudience.Kurinnyy;
        }

        if (role == SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy))
        {
            return PolicyAudience.Hurtkovyy;
        }

        if (HoldsOfficeIn(role, LeadershipType.Kurin))
        {
            return PolicyAudience.KurinProvid;
        }

        return HoldsOfficeIn(role, LeadershipType.Group) ? PolicyAudience.GroupProvid : null;
    }

    private static bool HoldsOfficeIn(string role, LeadershipType type) =>
        LeadershipOffices.Grouping[type].Any(office => role == SystemRole.ForOffice(type, office));
}
