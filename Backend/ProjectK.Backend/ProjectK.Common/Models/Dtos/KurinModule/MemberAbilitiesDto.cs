using System.Text.Json.Serialization;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>
/// What the caller may do to one member under the kurin's policy, so a client shows the buttons the
/// server would accept and no others. Levels are listed, because a policy may give the first
/// пересторога and keep the third.
/// </summary>
/// <param name="Warnings">Перестороги.</param>
/// <param name="Awards">Відзначення.</param>
/// <param name="Probes">Проби.</param>
/// <param name="Badges">Вмілості.</param>
/// <param name="Journal">Whether the caller may read this member's entries in the Журнал дій.</param>
public sealed record MemberAbilitiesDto(
    WarningAbilitiesDto Warnings,
    AwardAbilitiesDto Awards,
    ProbeAbilitiesDto Probes,
    BadgeAbilitiesDto Badges,
    bool Journal);

/// <param name="Give">The levels (1–3) the caller may give.</param>
/// <param name="Cancel">Whose перестороги the caller may cancel.</param>
public sealed record WarningAbilitiesDto(IReadOnlyList<int> Give, WarningCancelAbility Cancel);

/// <param name="Confirm">The levels (1–4) the caller may confirm.</param>
/// <param name="Remove">Whether the caller may remove a confirmed award or someone else's submission.</param>
public sealed record AwardAbilitiesDto(IReadOnlyList<int> Confirm, bool Remove);

/// <param name="Sign">Sign points, complete and verify.</param>
/// <param name="Unsign">Remove a signature, take a проба back from verified.</param>
public sealed record ProbeAbilitiesDto(bool Sign, bool Unsign);

/// <param name="Review">Confirm or return a submitted вмілість.</param>
public sealed record BadgeAbilitiesDto(bool Review);

/// <summary>Whose перестороги someone may cancel.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WarningCancelAbility>))]
public enum WarningCancelAbility
{
    /// <summary>None at all.</summary>
    None,

    /// <summary>Only those they gave themselves, of a level they may still give.</summary>
    OwnIssued,

    /// <summary>Anyone's.</summary>
    Any
}
