namespace ProjectK.Common.Models.Reports;

/// <summary>
/// The person's whole пластовий шлях as one document: where they stood, what they became and did,
/// section by section, oldest first. Words are settled when it is assembled, so the renderer only draws.
/// </summary>
public sealed record LifeStoryData(
    string FullName,
    DateTime GeneratedAtUtc,
    IReadOnlyList<LifeStorySection> Sections);

/// <summary>One part of the story — «Членства», «Ролі», «Вишколи». An empty one is left out.</summary>
public sealed record LifeStorySection(string Title, IReadOnlyList<LifeStoryRow> Rows);

/// <summary>A line: when, what, and whatever else names it — the kurin, the комендант, a note.</summary>
public sealed record LifeStoryRow(string When, string What, string? Detail);

/// <summary>The rendered life story and the name it downloads under.</summary>
public sealed record LifeStoryPdf(byte[] Content, string FileName);
