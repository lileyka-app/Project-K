using ProjectK.Common.Dtos.ProbesAndBadgesModule;

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

    /// <summary>The ступені and senior відзначення, ступені first.</summary>
    public IReadOnlyList<CatalogHonour> Honours
    {
        get => _honours;
        init
        {
            _honours = value;
            HonoursById = value.GroupBy(item => item.Id).ToDictionary(g => g.Key, g => g.First());
        }
    }

    public IReadOnlyDictionary<string, CatalogHonour> HonoursById { get; private init; } =
        new Dictionary<string, CatalogHonour>();

    private readonly IReadOnlyList<CatalogHonour> _honours = [];
}
