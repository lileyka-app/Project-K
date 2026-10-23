using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ProjectK.Common.Models.Authorization;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// Freezes what <see cref="RolePermissionMap.Resolve(IEnumerable{string})"/> grants each role under the
/// default kurin policy, so a refactor of the map cannot move a right without someone saying so.
/// The frozen output lives in <c>RolePermissionMap.golden.json</c> next to this file; a deliberate change is declared in
/// <see cref="IntendedChanges"/> with its reason instead of rewriting the fixture.
/// Regenerate the fixture on purpose (and empty <see cref="IntendedChanges"/>) with
/// <c>PROJECTK_REGENERATE_PERMISSION_GOLDEN=1 dotnet test --filter RolePermissionMapGoldenTests</c>.
/// </summary>
public class RolePermissionMapGoldenTests
{
    private const string FixtureName = "RolePermissionMap.golden.json";
    private const string RegenerateVariable = "PROJECTK_REGENERATE_PERMISSION_GOLDEN";

    private static readonly JsonSerializerOptions FixtureJson = new() { WriteIndented = true, NewLine = "\n" };

    /// <summary>
    /// Every difference from the fixture that is meant. <c>Roles</c> null means every role the fixture
    /// lists; a removal there drops the permission wherever it was granted.
    /// </summary>
    private static readonly IntendedChange[] IntendedChanges =
    [
        new(ChangeKind.Removed, "MemberAward:Update:Own",
            "Dead grant: reviewing one's own award was only stopped by RequireGroupLeadership."),
        new(ChangeKind.Removed, "BadgeProgress:Update:Own",
            "Dead grant: reviewing one's own badge was only stopped by RequireGroupLeadership."),

        new(ChangeKind.Added, "MemberContacts:Read:KurinWide",
            "Contacts became their own resource; by default everyone still sees them kurin-wide."),
        new(ChangeKind.Added, "MemberBirthDate:Read:KurinWide",
            "The birth date became its own resource; by default everyone still sees it kurin-wide."),
        new(ChangeKind.Added, "MemberBirthday:Read:KurinWide",
            "The birthday became its own resource; by default everyone still sees it kurin-wide."),
        .. PrivateDataReads("MemberAddress"),
        .. PrivateDataReads("MemberParents"),
        .. PrivateDataReads("MemberExtras"),
        new(ChangeKind.Added, "KurinJournal:Read:KurinWide",
            "New Журнал дій; the whole КВ reads it by default.",
            [Zvyazkovyi, Vykhovnyk, Instruktor, SystemRole.Admin]),
        new(ChangeKind.Added, "ProbeProgress:Delete:OwnGroups",
            "Unsigning a probe became its own right (UnsignProbe); the впорядник keeps it for his гурток.",
            [Vykhovnyk, Instruktor]),
        .. AdminCoversTheNewResources()
    ];

    private const string Zvyazkovyi = "KV.Zvyazkovyi";
    private const string Vykhovnyk = "KV.Vykhovnyk";
    private const string Instruktor = "KV.Instruktor";

    // Address, parents and the край's extra fields were scrubbed in the card for anyone but the
    // Звʼязковий and the гурток's впорядник; now that is a permission, at the same reach.
    private static IEnumerable<IntendedChange> PrivateDataReads(string resource) =>
    [
        new(ChangeKind.Added, $"{resource}:Read:KurinWide",
            "Private card data became its own resource; the Звʼязковий still sees it kurin-wide.",
            [Zvyazkovyi, SystemRole.Admin]),
        new(ChangeKind.Added, $"{resource}:Read:OwnGroups",
            "Private card data became its own resource; the впорядник still sees it for his гурток.",
            [Vykhovnyk, Instruktor])
    ];

    private static IEnumerable<IntendedChange> AdminCoversTheNewResources() =>
        new[] { "MemberContacts", "MemberBirthDate", "MemberBirthday", "MemberAddress", "MemberParents", "MemberExtras", "KurinJournal" }
            .SelectMany(resource => new[] { "Create", "Update", "Delete", "Manage" }
                .Select(action => new IntendedChange(ChangeKind.Added, $"{resource}:{action}:KurinWide",
                    "The admin holds every action on every resource, the new ones included.",
                    [SystemRole.Admin])));

    [Fact]
    public void Resolve_MatchesTheFrozenMap_PlusIntendedChanges()
    {
        var actual = CurrentMap();

        if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
        {
            File.WriteAllText(FixturePath(), JsonSerializer.Serialize(actual, FixtureJson) + "\n");
            return;
        }

        var frozen = ReadFixture();
        var expected = Apply(frozen, IntendedChanges, out var staleChanges);

        var report = new StringBuilder();
        foreach (var stale in staleChanges)
        {
            report.AppendLine($"Intended change no longer differs from the fixture: {stale}");
        }

        foreach (var role in expected.Keys.Union(actual.Keys).Order(StringComparer.Ordinal))
        {
            var want = expected.TryGetValue(role, out var e) ? e : [];
            var got = actual.TryGetValue(role, out var a) ? a : [];
            var missing = want.Except(got).Order(StringComparer.Ordinal).ToList();
            var extra = got.Except(want).Order(StringComparer.Ordinal).ToList();
            if (missing.Count == 0 && extra.Count == 0)
            {
                continue;
            }

            report.AppendLine(role);
            missing.ForEach(permission => report.AppendLine($"  - {permission}"));
            extra.ForEach(permission => report.AppendLine($"  + {permission}"));
        }

        Assert.True(
            report.Length == 0,
            "RolePermissionMap drifted from the frozen map (- lost, + gained). Declare a meant change in " +
            $"IntendedChanges, or regenerate with {RegenerateVariable}=1.\n{report}");
    }

    private static SortedDictionary<string, List<string>> CurrentMap()
    {
        var roles = LeadershipOffices.All()
            .Select(office => SystemRole.ForOffice(office.Type, office.Role))
            .Append(SystemRole.Member)
            .Append(SystemRole.Admin);

        var map = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            map[role] = RolePermissionMap.Resolve([role])
                .Select(permission => permission.ToClaimValue())
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        return map;
    }

    private static Dictionary<string, HashSet<string>> Apply(
        IReadOnlyDictionary<string, List<string>> frozen,
        IEnumerable<IntendedChange> changes,
        out List<IntendedChange> stale)
    {
        var expected = frozen.ToDictionary(pair => pair.Key, pair => pair.Value.ToHashSet(StringComparer.Ordinal));
        stale = [];

        foreach (var change in changes)
        {
            var applied = false;
            foreach (var role in change.Roles ?? expected.Keys.ToArray())
            {
                if (!expected.TryGetValue(role, out var permissions))
                {
                    permissions = new HashSet<string>(StringComparer.Ordinal);
                    expected[role] = permissions;
                }

                applied |= change.Kind == ChangeKind.Removed
                    ? permissions.Remove(change.Permission)
                    : permissions.Add(change.Permission);
            }

            if (!applied)
            {
                stale.Add(change);
            }
        }

        return expected;
    }

    private static Dictionary<string, List<string>> ReadFixture()
    {
        using var stream = typeof(RolePermissionMapGoldenTests).Assembly
            .GetManifestResourceStream($"{typeof(RolePermissionMapGoldenTests).Namespace}.{FixtureName}")
            ?? throw new InvalidOperationException($"{FixtureName} is not embedded in the test assembly.");

        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(stream)
            ?? throw new InvalidOperationException($"{FixtureName} is empty.");
    }

    private static string FixturePath([CallerFilePath] string testFile = "") =>
        Path.Combine(Path.GetDirectoryName(testFile)!, FixtureName);

    private enum ChangeKind
    {
        Added,
        Removed
    }

    private sealed record IntendedChange(ChangeKind Kind, string Permission, string Reason, string[]? Roles = null)
    {
        public override string ToString() =>
            $"{Kind} {Permission} for {(Roles is null ? "every role" : string.Join(", ", Roles))} ({Reason})";
    }
}
