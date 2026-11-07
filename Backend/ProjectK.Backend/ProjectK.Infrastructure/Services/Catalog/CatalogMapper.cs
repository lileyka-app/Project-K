using ProjectK.Common.Models.Catalog;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Services.Catalog;

/// <summary>
/// Turns the catalogue document into the shapes Lilejka's clients already read. Nothing is made up:
/// a field the catalogue leaves out stays empty.
/// </summary>
internal static class CatalogMapper
{
    private static readonly Dictionary<string, string> KraiNames = KraiSeed.All.ToDictionary(
        seed => seed.Code, seed => seed.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> Statuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["approved"] = "Затверджена",
        ["trial"] = "Апробаційна",
    };

    private static readonly Dictionary<string, string> UladNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["upn"] = "УПН",
        ["upyu"] = "УПЮ",
        ["usp"] = "УСП",
        ["ups"] = "УПС",
    };

    public static CatalogState ToState(CatalogDocument document, string sourceUrl, DateTimeOffset? fetchedAtUtc)
    {
        var categories = (document.Categories ?? [])
            .GroupBy(category => category.Id)
            .ToDictionary(group => group.Key, group => group.First().Title);

        var editions = (document.Editions ?? [])
            .GroupBy(edition => edition.Id)
            .ToDictionary(group => group.Key, group => group.First().Title);

        var badges = (document.Badges ?? []).Select(badge => ToBadge(badge, categories)).ToList();
        var probes = (document.Probes ?? [])
            .OrderBy(probe => probe.Order)
            .ThenBy(probe => probe.Edition, StringComparer.Ordinal)
            .Select(probe => ToProbe(probe, editions))
            .ToList();
        var awardImages = (document.Awards ?? [])
            .Where(award => award.Group == "upyu-vidznachennia" && award.Level is not null && award.Images is not null)
            .GroupBy(award => award.Level!.Value)
            .ToDictionary(group => group.Key, group => group.First().Images!);

        var honours = Honours(document);

        var metadata = new BadgesMetadata(
            ParserVersion: $"schema {document.SchemaVersion}, revision {document.Revision}",
            ToolAuthor: string.Empty,
            ParserComment: string.Empty,
            ParsedAtUtc: fetchedAtUtc,
            SourceUrl: sourceUrl,
            FixerEnabled: false,
            FixerMode: string.Empty,
            TotalBadges: badges.Count);

        return new CatalogState(document.Revision, metadata, badges, probes, awardImages) { Honours = honours };
    }

    // Вишколи are not відзначення and are not recorded on the card.
    private static readonly HashSet<string> TrainingGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "lsh", "shb", "kv-upn", "kv-upyu", "zb", "kvdch"
    };

    // The УПЮ відзначення stay out: they are recorded by level, with their pictures by level.
    private const string YouthAwardGroup = "upyu-vidznachennia";

    private static List<CatalogHonour> Honours(CatalogDocument document)
    {
        var degrees = (document.Degrees ?? [])
            .OrderBy(degree => UladRank(degree.Ulad))
            .ThenBy(degree => degree.Order)
            .Select(degree => new CatalogHonour(
                degree.Id,
                HonourKind.Degree,
                degree.Title,
                degree.Ulad ?? string.Empty,
                degree.Ulad ?? string.Empty,
                degree.Order,
                degree.Images?.Color ?? degree.Images?.Bw ?? string.Empty));

        var awards = (document.Awards ?? [])
            .Where(award => award.Group != YouthAwardGroup && !TrainingGroups.Contains(award.Group) && !string.IsNullOrWhiteSpace(award.Title))
            .Select((award, index) => new CatalogHonour(
                award.Id,
                HonourKind.Award,
                award.Title!,
                award.Group,
                award.Ulad ?? string.Empty,
                index,
                award.Images?.Color ?? award.Images?.Bw ?? string.Empty));

        return [.. degrees, .. awards];
    }

    // A senior's own ступені first, УСП before УПС; the youth ones after, for whoever records the whole way.
    private static int UladRank(string? ulad) => ulad?.ToLowerInvariant() switch
    {
        "usp" => 0,
        "ups" => 1,
        "upyu" => 2,
        _ => 3
    };

    private static Badge ToBadge(CatalogBadge badge, IReadOnlyDictionary<string, string> categories)
    {
        var requirements = string.Join("\n", (badge.Requirements ?? []).Select(r => $"{r.Id}. {r.Text}"));
        if (!string.IsNullOrWhiteSpace(badge.Author))
        {
            requirements += $"\n\nАвтор: {badge.Author}";
        }

        return new Badge(
            badge.Id,
            badge.Title,
            badge.Images?.Color ?? badge.Images?.Bw ?? string.Empty,
            badge.Krai is null ? string.Empty : KraiNames.GetValueOrDefault(badge.Krai, badge.Krai),
            string.Join(", ", (badge.Categories ?? []).Select(id => categories.GetValueOrDefault(id, id))),
            badge.Status is null ? string.Empty : Statuses.GetValueOrDefault(badge.Status, badge.Status),
            badge.Level,
            badge.UpdatedOn ?? string.Empty,
            requirements,
            badge.InstructorRequirements ?? string.Empty,
            [],
            [.. (badge.Requirements ?? []).Select(r => new BadgeRequirement(r.Id, r.Text))],
            badge.Author ?? string.Empty,
            [.. (badge.Ulady ?? []).Select(ulad => UladNames.GetValueOrDefault(ulad, ulad.ToUpperInvariant()))])
        {
            KraiCode = KraiCode(badge.Krai)
        };
    }

    private static Probe ToProbe(CatalogProbe probe, IReadOnlyDictionary<string, string> editions)
    {
        var sections = (probe.Sections ?? [])
            .Select(section => new ProbeSection(section.Id, section.Code, section.Title, ToPoints(section.Points)))
            .ToList();
        var own = ToPoints(probe.Points);

        var edition = string.IsNullOrWhiteSpace(probe.Edition) ? null : probe.Edition.Trim();

        return new Probe(probe.Id, probe.Title, own.Count > 0 ? own : [.. sections.SelectMany(s => s.Points)], sections)
        {
            Edition = edition,
            EditionTitle = edition is null ? null : editions.GetValueOrDefault(edition),
            Order = probe.Order,
            KraiCode = KraiCode(probe.Krai),
            StatusCode = probe.Status?.Trim().ToLowerInvariant() ?? string.Empty
        };
    }

    private static string KraiCode(string? krai) => krai?.Trim().ToLowerInvariant() ?? string.Empty;

    private static List<ProbePoint> ToPoints(IReadOnlyList<CatalogRequirement>? points)
        => [.. (points ?? []).Select(point => new ProbePoint(point.Id, point.Text))];
}
