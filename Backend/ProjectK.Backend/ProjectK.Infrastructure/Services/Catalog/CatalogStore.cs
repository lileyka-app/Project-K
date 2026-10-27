using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;

namespace ProjectK.Infrastructure.Services.Catalog;

/// <summary>One fetched catalogue, ready to read. Replaced whole, never edited.</summary>
internal sealed class CatalogState
{
    public static readonly CatalogState Empty = new(
        0,
        new BadgesMetadata(string.Empty, string.Empty, string.Empty, null, string.Empty, false, string.Empty, 0),
        [],
        [],
        new Dictionary<int, CatalogImages>());

    public CatalogState(
        long revision,
        BadgesMetadata metadata,
        IReadOnlyList<Badge> badges,
        IReadOnlyList<Probe> probes,
        IReadOnlyDictionary<int, CatalogImages> awardImages)
    {
        Revision = revision;
        Metadata = metadata;
        Badges = badges;
        Probes = probes;
        AwardImages = awardImages;
        BadgesById = badges.GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
        ProbesById = probes.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
    }

    public long Revision { get; }
    public BadgesMetadata Metadata { get; }
    public IReadOnlyList<Badge> Badges { get; }
    public IReadOnlyList<Probe> Probes { get; }
    public IReadOnlyDictionary<int, CatalogImages> AwardImages { get; }
    public IReadOnlyDictionary<string, Badge> BadgesById { get; }
    public IReadOnlyDictionary<string, Probe> ProbesById { get; }
}

/// <summary>
/// The catalogue in memory. <see cref="CatalogSyncBackgroundService"/> swaps in each new fetch;
/// readers always see one whole version.
/// <para>
/// The край names on вмілості are laid over the fetch here rather than in
/// <see cref="CatalogMapper"/>: they come from the <c>Krais</c> table, which changes without a new
/// fetch, and the fetch arrives without the table. Each of the two rebuilds what readers see.
/// </para>
/// </summary>
internal sealed class CatalogStore : IBadgesCatalog, IProbesCatalog, ICatalogKraiNames
{
    private readonly object _gate = new();
    private CatalogState _fetched = CatalogState.Empty;
    private Dictionary<string, string> _kraiNames = new(StringComparer.OrdinalIgnoreCase);
    private CatalogState _state = CatalogState.Empty;
    private long _version;

    public CatalogState State => Volatile.Read(ref _state);

    public long Version => Interlocked.Read(ref _version);

    public bool IsLoaded => State.Badges.Count > 0 || State.Probes.Count > 0;

    public void Replace(CatalogState state)
    {
        lock (_gate)
        {
            _fetched = state;
            Publish();
        }
    }

    public void Name(string kraiCode, string name)
    {
        lock (_gate)
        {
            if (_kraiNames.TryGetValue(kraiCode, out var current) && current == name)
            {
                return;
            }

            _kraiNames = new Dictionary<string, string>(_kraiNames, StringComparer.OrdinalIgnoreCase) { [kraiCode] = name };
            Publish();
        }
    }

    /// <summary>Every край at once, as the table holds them; a code it leaves out keeps its name.</summary>
    public void NameAll(IEnumerable<KeyValuePair<string, string>> namesByCode)
    {
        lock (_gate)
        {
            var names = new Dictionary<string, string>(_kraiNames, StringComparer.OrdinalIgnoreCase);
            foreach (var (code, name) in namesByCode)
            {
                names[code] = name;
            }

            if (names.Count == _kraiNames.Count && names.All(pair => _kraiNames.GetValueOrDefault(pair.Key) == pair.Value))
            {
                return;
            }

            _kraiNames = names;
            Publish();
        }
    }

    private void Publish()
    {
        var fetched = _fetched;
        var badges = fetched.Badges
            .Select(badge => _kraiNames.TryGetValue(badge.KraiCode, out var name) && name != badge.Country ? badge with { Country = name } : badge)
            .ToList();

        Volatile.Write(ref _state, new CatalogState(fetched.Revision, fetched.Metadata, badges, fetched.Probes, fetched.AwardImages));
        Interlocked.Increment(ref _version);
    }

    public BadgesMetadata Metadata => State.Metadata;

    IReadOnlyList<Badge> IBadgesCatalog.GetAll() => State.Badges;

    public Badge? GetById(string id) => State.BadgesById.GetValueOrDefault(id);

    IReadOnlyList<Probe> IProbesCatalog.GetAll() => State.Probes;

    Probe? IProbesCatalog.GetById(string id) => State.ProbesById.GetValueOrDefault(id);
}
