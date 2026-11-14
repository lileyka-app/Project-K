namespace ProjectK.Common.Dtos.MeModule;

/// <summary>
/// A card as anyone may see it: the name and only the parts the person chose and was allowed to
/// show. A part left out is null, not empty — empty means shown, but nothing there yet.
/// </summary>
public sealed record PublicCardDto(
    string FullName,
    string? PhotoUrl,
    IReadOnlyList<PublicCardKurinDto>? Kurins,
    string? Level,
    IReadOnlyList<PublicCardAwardDto>? Awards,
    IReadOnlyList<PublicCardBadgeDto>? Badges,
    IReadOnlyList<PublicCardProbeDto>? Probes,
    DateTime? CheckedAtUtc);
