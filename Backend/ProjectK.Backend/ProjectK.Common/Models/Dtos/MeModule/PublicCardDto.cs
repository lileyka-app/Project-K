using System.Text.Json.Serialization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.MeModule;

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
