using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Rules.IdentityModule;

/// <summary>
/// The access layer's role names. Office roles mirror the діловодство registry one-to-one
/// (<c>{провід}.{офіс}</c>, e.g. <c>KV.Zvyazkovyi</c>) and are worked out by
/// <c>IAccessContextResolver</c> from the offices held in the kurin the account is currently in.
/// <para>
/// They are <b>not</b> stored on the account. They used to be, and that made "виховник" a thing a
/// person was everywhere rather than in one kurin — which stops being true the moment someone
/// belongs to two. <see cref="Admin"/> is the one role the identity store still holds, because it
/// really does mean the same thing everywhere; <see cref="Member"/> is the baseline every
/// authenticated account carries.
/// </para>
/// </summary>
public static class SystemRoleNames
{
    public const string Admin = "Admin";
    public const string Member = "Member";

    /// <summary>The system-role name for an office within its провід.</summary>
    public static string ForOffice(LeadershipType type, LeadershipRole role) => $"{type}.{role}";

    /// <summary>Whether the role is an office in a гуртковий провід — some of which carry no right to manage the гурток.</summary>
    public static bool IsGroupOffice(string role) => role.StartsWith($"{LeadershipType.Group}.", StringComparison.Ordinal);

    /// <summary>Every role name that should exist in the identity store.</summary>
    public static IReadOnlyList<string> All() =>
        new[] { Admin, Member }
            .Concat(LeadershipOfficeRules.All().Select(office => ForOffice(office.Type, office.Role)))
            .ToArray();

    // Deliberately no "who is privileged" list here. That question is answered by
    // RolePermissionRules.GrantsWholeKurinManagement, derived from the grant tables — a second
    // hardcoded list drifted from it once already and left Курінний unable to disable an MFA
    // he was never required to enable.
}
