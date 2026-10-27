namespace ProjectK.Common.Models.Catalog;

/// <summary>
/// A вмілість as Lilejka has always shown it, plus the catalogue's own pieces the badge card shows.
/// <see cref="SeekerRequirements"/> still carries the requirements and the author as one text, so
/// clients that read only it keep working.
/// </summary>
public sealed record Badge(
    string Id,
    string Title,
    string ImagePath,
    string Country,
    string Specialization,
    string Status,
    int? Level,
    string LastUpdated,
    string SeekerRequirements,
    string InstructorRequirements,
    IReadOnlyList<string> FixNotes,
    IReadOnlyList<BadgeRequirement> Requirements,
    string Author,
    IReadOnlyList<string> Ulady)
{
    /// <summary>The catalogue's code of the край the вмілість belongs to; empty when the catalogue names none.</summary>
    public string KraiCode { get; init; } = string.Empty;
}

public sealed record BadgeRequirement(string Id, string Text);

public sealed record BadgesMetadata(
    string ParserVersion,
    string ToolAuthor,
    string ParserComment,
    DateTimeOffset? ParsedAtUtc,
    string SourceUrl,
    bool FixerEnabled,
    string FixerMode,
    int TotalBadges);

/// <summary>
/// A probe. <see cref="Points"/> are its own points or, when it has none, every point of its
/// sections in order: progress is recorded against those ids.
/// </summary>
public sealed record Probe(
    string Id,
    string Title,
    IReadOnlyList<ProbePoint> Points,
    IReadOnlyList<ProbeSection>? Sections = null)
{
    /// <summary>
    /// The edition of the programme the probe belongs to, e.g. "upyu-2026"; null for the probes
    /// every kurin has walked since before editions were named.
    /// </summary>
    public string? Edition { get; init; }

    /// <summary>
    /// The name the catalogue gives <see cref="Edition"/> for people, e.g. "Оновлена проба (2026)";
    /// null for the unnamed edition and for an edition the catalogue names nowhere.
    /// </summary>
    public string? EditionTitle { get; init; }

    /// <summary>The probe's place in its edition: probes are opened one after another by it.</summary>
    public int Order { get; init; }

    /// <summary>The catalogue's code of the край the probe belongs to; empty when it names none.</summary>
    public string KraiCode { get; init; } = string.Empty;

    /// <summary>"approved" or "trial", as the catalogue says; retired probes never reach Lilejka.</summary>
    public string StatusCode { get; init; } = string.Empty;
}

public sealed record ProbePoint(string Id, string Title);

public sealed record ProbeSection(string Id, string Code, string Title, IReadOnlyList<ProbePoint> Points);

/// <summary>An image downloaded from the catalogue.</summary>
public sealed record CatalogImage(byte[] Content, string ContentType);
