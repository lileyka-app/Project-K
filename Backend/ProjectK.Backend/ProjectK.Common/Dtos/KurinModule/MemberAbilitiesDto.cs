namespace ProjectK.Common.Dtos.KurinModule;

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
/// <param name="Dues">Whether the caller may read this member's вкладка, so a card does not ask for it in vain.</param>
public sealed record MemberAbilitiesDto(
    WarningAbilitiesDto Warnings,
    AwardAbilitiesDto Awards,
    ProbeAbilitiesDto Probes,
    BadgeAbilitiesDto Badges,
    bool Journal,
    bool Dues);
