using System.Text.Json;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Authorization.KurinPolicy;

/// <summary>
/// What one kurin has set in «Права й видимість»: the defaults of <see cref="KurinCapabilities"/>
/// with the kurin's own cells laid over them. Immutable; only the cells that differ from the
/// defaults are kept, so a default changed in code reaches every kurin that never touched it.
/// <para>
/// Stored in <c>Kurin.AccessPolicy</c> as <c>{"v":1,"grants":{"SeeContacts":{"Youth":"OwnGroup"}}}</c>.
/// Reading is tolerant (a name this build does not know is skipped, so an older build survives a
/// newer document); building from a request is strict (see <see cref="Validate"/>).
/// </para>
/// </summary>
public sealed class KurinAccessPolicy
{
    /// <summary>The document version this build writes and understands.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The error code a rejected policy is answered with.</summary>
    public const string InvalidErrorCode = "AccessPolicyInvalid";

    private const string VersionProperty = "v";
    private const string GrantsProperty = "grants";

    private readonly IReadOnlyDictionary<KurinCapability, IReadOnlyDictionary<PolicyAudience, CapabilityReach>> _overrides;

    private KurinAccessPolicy(IReadOnlyDictionary<KurinCapability, IReadOnlyDictionary<PolicyAudience, CapabilityReach>> overrides)
    {
        _overrides = overrides;
    }

    /// <summary>The policy of a kurin that has set nothing.</summary>
    public static KurinAccessPolicy Default { get; } =
        new(new Dictionary<KurinCapability, IReadOnlyDictionary<PolicyAudience, CapabilityReach>>());

    /// <summary>Whether the kurin has set nothing that differs from the defaults.</summary>
    public bool IsDefault => _overrides.Count == 0;

    /// <summary>The cells the kurin set that differ from the defaults, in table order.</summary>
    public IReadOnlyDictionary<KurinCapability, IReadOnlyDictionary<PolicyAudience, CapabilityReach>> Overrides => _overrides;

    /// <summary>How far <paramref name="capability"/> reaches for one audience.</summary>
    public CapabilityReach Reach(KurinCapability capability, PolicyAudience audience)
    {
        return _overrides.TryGetValue(capability, out var cells) && cells.TryGetValue(audience, out var reach)
            ? reach
            : KurinCapabilities.Get(capability).Defaults[audience];
    }

    /// <summary>The widest reach of <paramref name="capability"/> over every audience someone belongs to.</summary>
    public CapabilityReach Reach(KurinCapability capability, IEnumerable<PolicyAudience> audiences)
    {
        var widest = CapabilityReach.None;
        foreach (var audience in audiences)
        {
            var reach = Reach(capability, audience);
            if (reach > widest)
            {
                widest = reach;
            }
        }

        return widest;
    }

    /// <summary>
    /// The permissions this policy gives someone in <paramref name="audiences"/>: one per
    /// (resource, action) at the widest reach of the capabilities sharing it. Leveled capabilities
    /// share a pair, so the level itself is the handler's to check.
    /// </summary>
    public IEnumerable<Permission> Grants(IEnumerable<PolicyAudience> audiences)
    {
        var held = audiences as IReadOnlyCollection<PolicyAudience> ?? audiences.ToList();
        var widest = new Dictionary<(ResourceType, ResourceAction), CapabilityReach>();
        foreach (var descriptor in KurinCapabilities.All)
        {
            var pair = (descriptor.Resource, descriptor.Action);
            var reach = Reach(descriptor.Key, held);
            if (!widest.TryGetValue(pair, out var current) || reach > current)
            {
                widest[pair] = reach;
            }
        }

        foreach (var ((resource, action), reach) in widest)
        {
            if (reach.ToScope() is { } scope)
            {
                yield return new Permission(resource, action, scope);
            }
        }
    }

    /// <summary>
    /// Reads a stored document. Never throws: an empty column, broken JSON or a version this build
    /// does not know gives the defaults (reported through <paramref name="warn"/>); a capability,
    /// audience or reach it does not know, a locked cell and a cell equal to its default are skipped.
    /// </summary>
    public static KurinAccessPolicy Read(string? json, Action<string>? warn = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(VersionProperty, out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var number)
                || number != SchemaVersion)
            {
                warn?.Invoke($"Access policy document is not version {SchemaVersion}; using the defaults.");
                return Default;
            }

            if (!root.TryGetProperty(GrantsProperty, out var grants) || grants.ValueKind != JsonValueKind.Object)
            {
                return Default;
            }

            var overrides = new Dictionary<KurinCapability, Dictionary<PolicyAudience, CapabilityReach>>();
            foreach (var row in grants.EnumerateObject())
            {
                if (!TryName<KurinCapability>(row.Name, out var capability) || row.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var descriptor = KurinCapabilities.Get(capability);
                foreach (var cell in row.Value.EnumerateObject())
                {
                    if (TryName<PolicyAudience>(cell.Name, out var audience)
                        && cell.Value.ValueKind == JsonValueKind.String
                        && TryName<CapabilityReach>(cell.Value.GetString(), out var reach)
                        && !descriptor.IsLocked(audience)
                        && reach != descriptor.Defaults[audience])
                    {
                        Cells(overrides, capability)[audience] = reach;
                    }
                }
            }

            return From(overrides);
        }
        catch (JsonException)
        {
            warn?.Invoke("Access policy document is not valid JSON; using the defaults.");
            return Default;
        }
    }

    /// <summary>The stored form, or null when nothing differs from the defaults.</summary>
    public string? Write()
    {
        if (IsDefault)
        {
            return null;
        }

        var grants = _overrides.ToDictionary(
            row => row.Key.ToString(),
            row => row.Value.ToDictionary(cell => cell.Key.ToString(), cell => cell.Value.ToString()));

        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [VersionProperty] = SchemaVersion,
            [GrantsProperty] = grants
        });
    }

    /// <summary>
    /// What is wrong with a matrix sent to be saved, empty when nothing is. A matrix is
    /// capability → audience → reach by name; a cell left out keeps its default. Rejected: a name
    /// this build does not know, a missing reach, and any change to a locked cell — the
    /// Звʼязковий's column, and the Youth column of an action row.
    /// </summary>
    public static IReadOnlyList<string> Validate(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> matrix)
    {
        var errors = new List<string>();
        foreach (var (capabilityName, cells) in matrix)
        {
            if (!TryName<KurinCapability>(capabilityName, out var capability))
            {
                errors.Add($"Unknown capability '{capabilityName}'.");
                continue;
            }

            var descriptor = KurinCapabilities.Get(capability);
            foreach (var (audienceName, reachName) in cells)
            {
                if (!TryName<PolicyAudience>(audienceName, out var audience))
                {
                    errors.Add($"Unknown audience '{audienceName}' for {capability}.");
                }
                else if (!TryName<CapabilityReach>(reachName, out var reach))
                {
                    errors.Add($"Unknown reach '{reachName}' for {capability}/{audience}.");
                }
                else if (descriptor.IsLocked(audience) && reach != descriptor.Defaults[audience])
                {
                    errors.Add(audience == PolicyAudience.Youth
                        ? $"{capability} is not given to youths."
                        : $"{capability} for {audience} is locked.");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Builds the policy a full effective matrix describes, keeping only what differs from the
    /// defaults. Throws when <see cref="Validate"/> finds anything wrong with it.
    /// </summary>
    public static KurinAccessPolicy FromMatrix(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> matrix)
    {
        var errors = Validate(matrix);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(matrix));
        }

        var overrides = new Dictionary<KurinCapability, Dictionary<PolicyAudience, CapabilityReach>>();
        foreach (var (capabilityName, cells) in matrix)
        {
            var capability = Enum.Parse<KurinCapability>(capabilityName);
            var descriptor = KurinCapabilities.Get(capability);
            foreach (var (audienceName, reachName) in cells)
            {
                var audience = Enum.Parse<PolicyAudience>(audienceName);
                var reach = Enum.Parse<CapabilityReach>(reachName!);
                if (reach != descriptor.Defaults[audience])
                {
                    Cells(overrides, capability)[audience] = reach;
                }
            }
        }

        return From(overrides);
    }

    private static Dictionary<PolicyAudience, CapabilityReach> Cells(
        Dictionary<KurinCapability, Dictionary<PolicyAudience, CapabilityReach>> overrides,
        KurinCapability capability)
    {
        if (!overrides.TryGetValue(capability, out var cells))
        {
            cells = new Dictionary<PolicyAudience, CapabilityReach>();
            overrides[capability] = cells;
        }

        return cells;
    }

    private static KurinAccessPolicy From(Dictionary<KurinCapability, Dictionary<PolicyAudience, CapabilityReach>> overrides)
    {
        if (overrides.Count == 0)
        {
            return Default;
        }

        var ordered = KurinCapabilities.All
            .Where(descriptor => overrides.ContainsKey(descriptor.Key))
            .ToDictionary(
                descriptor => descriptor.Key,
                descriptor => (IReadOnlyDictionary<PolicyAudience, CapabilityReach>)KurinCapabilities.Audiences
                    .Where(overrides[descriptor.Key].ContainsKey)
                    .ToDictionary(audience => audience, audience => overrides[descriptor.Key][audience]));

        return new KurinAccessPolicy(ordered);
    }

    // Exact names only: Enum.TryParse would also take "1" or "kurin", and a stored number would
    // silently change meaning the day the enum is reordered.
    private static bool TryName<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum
    {
        if (name is not null && Enum.GetNames<TEnum>().Contains(name, StringComparer.Ordinal))
        {
            value = Enum.Parse<TEnum>(name);
            return true;
        }

        value = default;
        return false;
    }
}
