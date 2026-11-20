namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// The person's whole пластовий шлях as one document: where they stood, what they became and did,
/// section by section, oldest first. Words are settled when it is assembled, so the renderer only draws.
/// </summary>
public sealed record LifeStoryData(
    string FullName,
    DateTime GeneratedAtUtc,
    IReadOnlyList<LifeStorySection> Sections);
