using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Authorization;

/// <summary>
/// The single source of truth for what each <see cref="SystemRole"/> may do. To grant new access,
/// change a grant list here — nothing else reasons about role names.
/// <list type="bullet">
/// <item><b>Зв'язковий (КВ)</b> — full kurin management incl. kurin settings and office assignment.</item>
/// <item><b>Виховник (КВ)</b> — runs his гурток: members, progress, the group record and its провід.</item>
/// <item><b>Інструктор (КВ)</b> — the Виховник's grants without a гурток, so only the kurin-wide ones reach anything.</item>
/// <item><b>Курінний</b> — reads the kurin, plans, raises agenda items and assigns the курінний провід
/// below himself. He holds nothing on members: probe sign-off, awards, warnings and profile
/// verification all sit behind <c>Member:Update</c>, which he does not get.</item>
/// <item><b>Гуртковий</b> — the same, scoped to his гурток.</item>
/// <item>the rest of a провід — as their head, minus office assignment.</item>
/// <item><b>Скарбник</b> — his провід's grants plus the вкладка: the гурток's box for a гуртковий
/// скарбник, the kurin's box for a курінний one. Гуртковий and Виховник keep the гурток's box too;
/// only the Виховник marks an operation verified.</item>
/// <item><b>Суддя</b> — his провід's grants plus точкування: the гурток's sheets for a гуртковий суддя,
/// every гурток's and the kurin's rules for a курінний one. Курінний, Гуртковий and Виховник score
/// within their reach too; the Виховник and the Зв'язковий also keep the КВ's private score.</item>
/// <item>the bare <b>Member</b> — read within the kurin plus own profile/progress.</item>
/// </list>
/// The pairs listed in <see cref="KurinCapabilities"/> are not granted here: each kurin sets them in
/// its <see cref="KurinAccessPolicy"/>, and <see cref="Resolve(IEnumerable{string}, KurinAccessPolicy)"/>
/// adds them on top. Only the self-service <c>Own</c> grants of those pairs stay static.
/// </summary>
public static class RolePermissionMap
{
    private static readonly ResourceType[] AllResources = Enum.GetValues<ResourceType>();

    // Money is the one thing not open to the whole kurin: a youth sees his own balance, not the
    // гурток's debts. These stay out of the baseline read and are granted office by office.
    private static readonly ResourceType[] DuesResources = [ResourceType.GroupDues, ResourceType.KurinDues];

    // A youth's points are his own business and his гурток's провід's; the table of гуртки is
    // everyone's. So the гурток's score and the КВ's private one stay out of the baseline read.
    private static readonly ResourceType[] ScoreResources = [ResourceType.GroupScore, ResourceType.KurinScorePrivate];

    private static readonly ResourceAction[] DuesKeeping =
        [ResourceAction.Read, ResourceAction.Create, ResourceAction.Update, ResourceAction.Delete];

    private static readonly ResourceAction[] ScoreKeeping = DuesKeeping;

    // Baseline every authenticated member carries: read anything in the kurin, edit own profile,
    // submit own badge progress.
    private static readonly IReadOnlyList<Permission> MemberGrants = BuildMemberGrants();

    // Курінний провід: plan and raise agenda for the kurin, then own what they raised.
    private static readonly IReadOnlyList<Permission> KurinProvidGrants =
        BuildProvidGrants(AccessScope.KurinWide);

    // Гуртковий провід: the same, with planning bounded to their гурток.
    private static readonly IReadOnlyList<Permission> GroupProvidGrants =
        BuildProvidGrants(AccessScope.OwnGroups);

    // Курінний: провід baseline plus office assignment, bounded by AssignableOffices.
    private static readonly IReadOnlyList<Permission> KurinnyyGrants =
        WithActions(
            Append(KurinProvidGrants, new Permission(ResourceType.Leadership, ResourceAction.Update, AccessScope.KurinWide)),
            ResourceType.GroupScore, AccessScope.KurinWide, ScoreKeeping);

    // Гуртковий: as Курінний, bounded to his гурток, and he keeps the гурток's box.
    private static readonly IReadOnlyList<Permission> HurtkovyyGrants =
        WithActions(
            WithActions(
                Append(GroupProvidGrants, new Permission(ResourceType.Leadership, ResourceAction.Update, AccessScope.OwnGroups)),
                ResourceType.GroupDues, AccessScope.OwnGroups, DuesKeeping),
            ResourceType.GroupScore, AccessScope.OwnGroups, ScoreKeeping);

    // Гуртковий скарбник: the гурток's провід, and the гурток's box.
    private static readonly IReadOnlyList<Permission> GroupSkarbnykGrants =
        WithActions(GroupProvidGrants, ResourceType.GroupDues, AccessScope.OwnGroups, DuesKeeping);

    // Курінний скарбник: the kurin's провід and box; every гурток's box to read what it owes up.
    private static readonly IReadOnlyList<Permission> KurinSkarbnykGrants =
        WithActions(
            WithActions(KurinProvidGrants, ResourceType.KurinDues, AccessScope.KurinWide, DuesKeeping),
            ResourceType.GroupDues, AccessScope.KurinWide, [ResourceAction.Read]);

    // Гуртковий суддя: the гурток's провід, and its точкування.
    private static readonly IReadOnlyList<Permission> GroupSuddyaGrants =
        WithActions(GroupProvidGrants, ResourceType.GroupScore, AccessScope.OwnGroups, ScoreKeeping);

    // Курінний суддя: the kurin's провід; every гурток's точкування, and the rules it is scored by.
    private static readonly IReadOnlyList<Permission> KurinSuddyaGrants =
        WithActions(
            WithActions(KurinProvidGrants, ResourceType.GroupScore, AccessScope.KurinWide, ScoreKeeping),
            ResourceType.KurinScore, AccessScope.KurinWide, [ResourceAction.Manage]);

    // Виховник: runs his гурток — members, progress and the group record itself.
    private static readonly IReadOnlyList<Permission> VykhovnykGrants = BuildVykhovnykGrants();

    // Зв'язковий: full kurin management (irreversible Kurin Delete/Manage stays Admin-only).
    private static readonly IReadOnlyList<Permission> ZvyazkovyiGrants = BuildZvyazkovyiGrants();

    // Admin: everything, kurin-wide. The scope check still pins a stepped-in admin to their kurin.
    private static readonly IReadOnlyList<Permission> AdminGrants = BuildAdminGrants();

    /// <summary>
    /// Expands a set of role names into the union of their permissions under the default kurin
    /// policy. Where the kurin is known, resolve against its own policy instead.
    /// </summary>
    public static IReadOnlyCollection<Permission> Resolve(IEnumerable<string> roles) =>
        Resolve(roles, KurinAccessPolicy.Default);

    /// <summary>
    /// Expands a set of role names into the union of their permissions in a kurin with
    /// <paramref name="policy"/>: the static grants, plus what the policy gives their audiences.
    /// </summary>
    public static IReadOnlyCollection<Permission> Resolve(IEnumerable<string> roles, KurinAccessPolicy policy)
    {
        var held = roles as IReadOnlyCollection<string> ?? roles.ToList();
        var permissions = CollectStaticGrants(held);
        permissions.UnionWith(policy.Grants(PolicyAudiences.For(held)));
        return permissions;
    }

    /// <summary>
    /// The grants that do not depend on any kurin's policy. Holds no configurable pair except the
    /// self-service <c>Own</c> ones — and, for the admin, everything.
    /// </summary>
    public static IReadOnlySet<Permission> StaticGrants(IEnumerable<string> roles) => CollectStaticGrants(roles);

    private static HashSet<Permission> CollectStaticGrants(IEnumerable<string> roles)
    {
        var permissions = new HashSet<Permission>();
        foreach (var role in roles)
        {
            permissions.UnionWith(GrantsFor(role));
        }

        return permissions;
    }

    /// <summary>
    /// The offices <paramref name="roles"/> may put people into or take them out of. Only the head of
    /// each провід assigns, and never his own office — so a Писар cannot touch the Скарбник.
    /// </summary>
    public static IReadOnlySet<(LeadershipType Type, LeadershipRole Role)> AssignableOffices(IEnumerable<string> roles)
    {
        var assignable = new HashSet<(LeadershipType, LeadershipRole)>();
        foreach (var role in roles)
        {
            if (role.Equals(SystemRole.Admin, StringComparison.OrdinalIgnoreCase)
                || role == SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi))
            {
                foreach (var office in LeadershipOffices.All())
                {
                    assignable.Add(office);
                }

                continue;
            }

            // The впорядник is who appoints a гурток's провід, its Гуртковий included. Which гуртки
            // is the resource check's business: Leadership:Update reaches only those he leads.
            if (role == SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk))
            {
                foreach (var office in LeadershipOffices.Grouping[LeadershipType.Group])
                {
                    assignable.Add((LeadershipType.Group, office));
                }

                continue;
            }

            if (role == SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy))
            {
                AddBodyExceptHead(assignable, LeadershipType.Kurin, LeadershipRole.Kurinnuy);
            }
            else if (role == SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy))
            {
                AddBodyExceptHead(assignable, LeadershipType.Group, LeadershipRole.Hurtkoviy);
            }
        }

        return assignable;
    }

    /// <summary>Whether the roles grant whole-kurin management (Зв'язковий/admin).</summary>
    public static bool GrantsWholeKurinManagement(IEnumerable<string> roles) =>
        WidestScope(Resolve(roles), ResourceType.Group, ResourceAction.Manage) == AccessScope.KurinWide;

    /// <summary>Whether the roles grant group leadership at any scope (Виховник or higher).</summary>
    public static bool GrantsGroupLeadership(IEnumerable<string> roles) =>
        WidestScope(Resolve(roles), ResourceType.Group, ResourceAction.Update) is not null;

    /// <summary>
    /// Whether the roles may raise agenda items. Creation has no existing resource to scope against,
    /// so it is gated as a capability here and the target kurin is checked separately.
    /// </summary>
    public static bool GrantsAgendaAuthoring(IEnumerable<string> roles) =>
        WidestScope(Resolve(roles), ResourceType.AgendaItem, ResourceAction.Create) is not null;

    /// <summary>Whether the roles may open a planning session. Gated as a capability, like agenda creation.</summary>
    public static bool GrantsPlanningAuthoring(IEnumerable<string> roles) =>
        WidestScope(Resolve(roles), ResourceType.PlanningSession, ResourceAction.Create) is not null;

    /// <summary>The widest scope at which <paramref name="permissions"/> grants (resource, action), if any.</summary>
    public static AccessScope? WidestScope(
        IEnumerable<Permission> permissions,
        ResourceType resource,
        ResourceAction action)
    {
        AccessScope? widest = null;
        foreach (var permission in permissions)
        {
            if (permission.Resource != resource || permission.Action != action)
            {
                continue;
            }

            if (widest is null || permission.Scope > widest)
            {
                widest = permission.Scope;
            }
        }

        return widest;
    }

    private static IReadOnlyList<Permission> GrantsFor(string role)
    {
        if (role.Equals(SystemRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return AdminGrants;
        }

        if (role == SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi))
        {
            return ZvyazkovyiGrants;
        }

        // An інструктор is the КВ without a гурток of his own: the same grants, and every OwnGroups
        // scope among them reaches nothing until a гурток is assigned, which makes him a впорядник.
        if (role == SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk)
            || role == SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Instruktor))
        {
            return VykhovnykGrants;
        }

        if (role == SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy))
        {
            return KurinnyyGrants;
        }

        if (role == SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy))
        {
            return HurtkovyyGrants;
        }

        if (role == SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Skarbnyk))
        {
            return GroupSkarbnykGrants;
        }

        if (role == SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Skarbnyk))
        {
            return KurinSkarbnykGrants;
        }

        if (role == SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Suddya))
        {
            return GroupSuddyaGrants;
        }

        if (role == SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Suddya))
        {
            return KurinSuddyaGrants;
        }

        if (IsProvidOffice(role, LeadershipType.Kurin, LeadershipRole.Kurinnuy))
        {
            return KurinProvidGrants;
        }

        if (IsProvidOffice(role, LeadershipType.Group, LeadershipRole.Hurtkoviy))
        {
            return GroupProvidGrants;
        }

        // Every non-leading office: baseline only.
        return MemberGrants;
    }

    private static bool IsProvidOffice(string role, LeadershipType type, LeadershipRole head) =>
        LeadershipOffices.Grouping[type]
            .Where(office => office != head)
            .Any(office => role == SystemRole.ForOffice(type, office));

    private static void AddBodyExceptHead(
        HashSet<(LeadershipType, LeadershipRole)> assignable,
        LeadershipType type,
        LeadershipRole head)
    {
        foreach (var office in LeadershipOffices.Grouping[type].Where(office => office != head))
        {
            assignable.Add((type, office));
        }
    }

    private static IReadOnlyList<Permission> Append(IReadOnlyList<Permission> grants, Permission extra)
    {
        var combined = new List<Permission>(grants) { extra };
        return combined;
    }

    private static IReadOnlyList<Permission> WithActions(
        IReadOnlyList<Permission> grants,
        ResourceType resource,
        AccessScope scope,
        IEnumerable<ResourceAction> actions)
    {
        var combined = new List<Permission>(grants);
        combined.AddRange(actions.Select(action => new Permission(resource, action, scope)));
        return combined;
    }

    private static List<Permission> BuildMemberGrants()
    {
        var grants = new List<Permission>();
        var policyRead = AllResources.Where(resource => KurinCapabilities.IsConfigurable(resource, ResourceAction.Read));
        foreach (var resource in AllResources.Except(DuesResources).Except(ScoreResources).Except(policyRead))
        {
            grants.Add(new Permission(resource, ResourceAction.Read, AccessScope.KurinWide));
        }

        grants.Add(new Permission(ResourceType.GroupDues, ResourceAction.Read, AccessScope.Own));
        grants.Add(new Permission(ResourceType.GroupScore, ResourceAction.Read, AccessScope.Own));
        grants.Add(new Permission(ResourceType.Member, ResourceAction.Update, AccessScope.Own));
        grants.Add(new Permission(ResourceType.BadgeProgress, ResourceAction.Create, AccessScope.Own));
        grants.Add(new Permission(ResourceType.MemberAward, ResourceAction.Create, AccessScope.Own));
        grants.Add(new Permission(ResourceType.MemberAward, ResourceAction.Delete, AccessScope.Own));
        return grants;
    }

    /// <summary>
    /// What every провід member gets: plan and raise agenda items, then edit or drop the ones they
    /// authored. Agenda creation is always kurin-wide — who sees an item is decided by its assignment,
    /// not by the author's scope.
    /// </summary>
    private static List<Permission> BuildProvidGrants(AccessScope planningScope)
    {
        var grants = new List<Permission>(MemberGrants)
        {
            new(ResourceType.PlanningSession, ResourceAction.Create, planningScope),
            new(ResourceType.PlanningSession, ResourceAction.Update, AccessScope.Own),
            new(ResourceType.PlanningSession, ResourceAction.Delete, AccessScope.Own),
            new(ResourceType.AgendaItem, ResourceAction.Create, AccessScope.KurinWide),
            new(ResourceType.AgendaItem, ResourceAction.Update, AccessScope.Own),
            new(ResourceType.AgendaItem, ResourceAction.Delete, AccessScope.Own)
        };

        return grants;
    }

    private static List<Permission> BuildVykhovnykGrants()
    {
        var grants = new List<Permission>(GroupProvidGrants);
        var scopedResources = new[]
        {
            ResourceType.Group, ResourceType.Member, ResourceType.BadgeProgress, ResourceType.ProbeProgress,
            ResourceType.MemberWarning, ResourceType.MemberAward
        };

        foreach (var resource in scopedResources)
        {
            grants.Add(new Permission(resource, ResourceAction.Create, AccessScope.OwnGroups));
            grants.Add(new Permission(resource, ResourceAction.Update, AccessScope.OwnGroups));
        }

        // He seats his гурток's провід.
        grants.Add(new Permission(ResourceType.Leadership, ResourceAction.Update, AccessScope.OwnGroups));

        // He moderates everything aimed at his гурток, agenda included — not just what he authored.
        grants.Add(new Permission(ResourceType.AgendaItem, ResourceAction.Update, AccessScope.OwnGroups));
        grants.Add(new Permission(ResourceType.AgendaItem, ResourceAction.Delete, AccessScope.OwnGroups));

        // He keeps the гурток's box with the скарбник, and he alone marks an operation verified.
        grants.AddRange(DuesKeeping.Select(action => new Permission(ResourceType.GroupDues, action, AccessScope.OwnGroups)));
        grants.Add(new Permission(ResourceType.GroupDues, ResourceAction.Manage, AccessScope.OwnGroups));

        // He scores his гурток, and with the other впорядники keeps the КВ's own score.
        grants.AddRange(ScoreKeeping.Select(action => new Permission(ResourceType.GroupScore, action, AccessScope.OwnGroups)));
        grants.AddRange(ScoreKeeping.Select(action => new Permission(ResourceType.KurinScorePrivate, action, AccessScope.KurinWide)));
        return WithoutPolicyPairs(grants);
    }

    private static List<Permission> BuildZvyazkovyiGrants()
    {
        var grants = new List<Permission>(MemberGrants);
        var managedResources = new[]
        {
            ResourceType.Group, ResourceType.Member, ResourceType.PlanningSession, ResourceType.AgendaItem,
            ResourceType.BadgeProgress, ResourceType.ProbeProgress, ResourceType.Leadership,
            ResourceType.MemberWarning, ResourceType.MemberAward, ResourceType.GroupDues, ResourceType.KurinDues,
            ResourceType.GroupScore, ResourceType.KurinScore, ResourceType.KurinScorePrivate
        };
        var manageActions = new[]
        {
            ResourceAction.Create, ResourceAction.Update, ResourceAction.Delete, ResourceAction.Manage
        };

        foreach (var resource in managedResources)
        {
            foreach (var action in manageActions)
            {
                grants.Add(new Permission(resource, action, AccessScope.KurinWide));
            }
        }

        // The baseline leaves money out of what everyone reads; he reads all of it.
        grants.AddRange(DuesResources.Concat(ScoreResources).Select(resource => new Permission(resource, ResourceAction.Read, AccessScope.KurinWide)));

        // Kurin settings, but not the irreversible Delete/Manage (Admin-only).
        grants.Add(new Permission(ResourceType.Kurin, ResourceAction.Create, AccessScope.KurinWide));
        grants.Add(new Permission(ResourceType.Kurin, ResourceAction.Update, AccessScope.KurinWide));
        return WithoutPolicyPairs(grants);
    }

    // The kurin's policy decides these; granting them here too would make its "no" mean nothing.
    private static List<Permission> WithoutPolicyPairs(IEnumerable<Permission> grants) =>
        grants
            .Where(grant => grant.Scope == AccessScope.Own || !KurinCapabilities.IsConfigurable(grant.Resource, grant.Action))
            .ToList();

    private static List<Permission> BuildAdminGrants()
    {
        var grants = new List<Permission>();
        foreach (var resource in AllResources)
        {
            foreach (var action in Enum.GetValues<ResourceAction>())
            {
                grants.Add(new Permission(resource, action, AccessScope.KurinWide));
            }
        }

        return grants;
    }
}
