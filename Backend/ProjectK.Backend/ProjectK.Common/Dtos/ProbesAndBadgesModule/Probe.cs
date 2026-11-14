namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

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
