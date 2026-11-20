namespace ProjectK.Common.Dtos.MemberModule;

/// <summary>
/// A card as anyone may see it: the name and only the parts the person chose and was allowed to
/// show. A part left out is null, not empty — empty means shown, but nothing there yet. The ступінь
/// comes with the catalogue's picture of it, as the відзначення and вмілості come with theirs.
/// </summary>
public sealed record PublicCardDto(
    string FullName,
    string? PhotoUrl,
    IReadOnlyList<PublicCardKurinDto>? Kurins,
    string? Level,
    string? LevelImageUrl,
    IReadOnlyList<PublicCardAwardDto>? Awards,
    IReadOnlyList<PublicCardBadgeDto>? Badges,
    IReadOnlyList<PublicCardProbeDto>? Probes,
    DateTime? CheckedAtUtc);
