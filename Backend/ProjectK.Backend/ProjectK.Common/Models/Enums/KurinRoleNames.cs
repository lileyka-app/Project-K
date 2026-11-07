using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ProjectK.Common.Models.Enums;

/// <summary>
/// A kurin's own names for the roles it already has: писар becomes «скриптор». Only the name changes;
/// rights still come from the role through <c>RolePermissionMap</c>, so a kurin can rename what it
/// has but never make a role that grants something new. Stored on the kurin as
/// <c>{"Pysar":"Скриптор"}</c>; a role left out keeps its usual name.
/// </summary>
public static class KurinRoleNames
{
    public const int MaxLength = 60;
    public const string InvalidCode = "InvalidRoleNames";
    public const string NotAvailableCode = "RoleNamesNotAvailable";

    /// <summary>
    /// The roles a kurin may rename: its own offices. Гурткові roles stay out, since a kurin that
    /// renames roles has no гуртки.
    /// </summary>
    public static readonly IReadOnlySet<LeadershipRole> Renamable = new HashSet<LeadershipRole>
    {
        LeadershipRole.Kurinnuy, LeadershipRole.Suddya, LeadershipRole.Pysar, LeadershipRole.Skarbnyk,
        LeadershipRole.Horunjiy, LeadershipRole.Gospodar, LeadershipRole.Hronikar, LeadershipRole.Instruktor,
        LeadershipRole.Vykhovnyk, LeadershipRole.Zvyazkovyi, LeadershipRole.OtherKurin
    };

    /// <summary>
    /// Whether this kurin may rename its roles. Senior kurins for now; the one place to open it to
    /// УПЮ later.
    /// </summary>
    public static bool IsAvailableFor(KurinBranch branch) => KurinBranchSet.IsSenior(branch);

    /// <summary>The kurin's names by role; whatever it cannot read it skips, so a bad row costs a name, not the page.</summary>
    public static IReadOnlyDictionary<LeadershipRole, string> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<LeadershipRole, string>();
        }

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            return raw
                .Where(pair => Enum.TryParse<LeadershipRole>(pair.Key, out var role) && Renamable.Contains(role) && !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => Enum.Parse<LeadershipRole>(pair.Key), pair => pair.Value.Trim());
        }
        catch (JsonException)
        {
            return new Dictionary<LeadershipRole, string>();
        }
    }

    /// <summary>
    /// Checks names as the settings send them and turns them into what the kurin stores: blank names
    /// dropped, the rest trimmed. Null when something cannot be a role name.
    /// </summary>
    public static string? Normalize(IReadOnlyDictionary<string, string?> names, out bool valid)
    {
        valid = true;
        var kept = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in names)
        {
            if (!Enum.TryParse<LeadershipRole>(key, out var role) || !Renamable.Contains(role))
            {
                valid = false;
                return null;
            }

            var name = value?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.Length > MaxLength)
            {
                valid = false;
                return null;
            }

            kept[role.ToString()] = name;
        }

        return kept.Count == 0 ? null : JsonSerializer.Serialize(kept, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
