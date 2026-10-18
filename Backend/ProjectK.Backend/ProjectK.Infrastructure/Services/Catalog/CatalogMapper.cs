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

        var badges = (document.Badges ?? []).Select(badge => ToBadge(badge, categories)).ToList();
        var probes = (document.Probes ?? []).OrderBy(probe => probe.Order).Select(ToProbe).ToList();
        var awardImages = (document.Awards ?? [])
            .Where(award => award.Group == "upyu-vidznachennia" && award.Level is not null && award.Images is not null)
            .GroupBy(award => award.Level!.Value)
            .ToDictionary(group => group.Key, group => group.First().Images!);

        var metadata = new BadgesMetadata(
            ParserVersion: $"schema {document.SchemaVersion}, revision {document.Revision}",
            ToolAuthor: string.Empty,
            ParserComment: string.Empty,
            ParsedAtUtc: fetchedAtUtc,
            SourceUrl: sourceUrl,
            FixerEnabled: false,
            FixerMode: string.Empty,
            TotalBadges: badges.Count);

        return new CatalogState(document.Revision, metadata, badges, probes, awardImages);
    }

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

    private static Probe ToProbe(CatalogProbe probe)
    {
        var sections = (probe.Sections ?? [])
            .Select(section => new ProbeSection(section.Id, section.Code, section.Title, ToPoints(section.Points)))
            .ToList();
        var own = ToPoints(probe.Points);

        return new Probe(probe.Id, probe.Title, own.Count > 0 ? own : [.. sections.SelectMany(s => s.Points)], sections);
    }

    private static string KraiCode(string? krai) => krai?.Trim().ToLowerInvariant() ?? string.Empty;

    private static List<ProbePoint> ToPoints(IReadOnlyList<CatalogRequirement>? points)
        => [.. (points ?? []).Select(point => new ProbePoint(point.Id, point.Text))];
}
