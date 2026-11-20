namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

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
