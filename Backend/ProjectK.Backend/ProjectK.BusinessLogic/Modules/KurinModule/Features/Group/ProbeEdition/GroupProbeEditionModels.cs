namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.ProbeEdition;

/// <summary>
/// The probe edition of every гурток in a kurin, for the kurin's settings: the editions the
/// catalogue offers its край and, per гурток, where each youth stands.
/// </summary>
public sealed record KurinProbeEditionsDto(
    IReadOnlyList<ProbeEditionDto> Editions,
    IReadOnlyList<GroupProbeEditionDto> Groups);

/// <param name="Id">The catalogue id of the edition, e.g. "upyu-2026"; null for the unnamed one.</param>
/// <param name="Title">The name the catalogue gives the edition; null for the unnamed one, which the
/// clients call «Звичайна проба» themselves.</param>
/// <param name="IsTrial">The catalogue still marks the edition as in апробація.</param>
/// <param name="ProbeTitles">Its probes in order, so the choice says what it is.</param>
public sealed record ProbeEditionDto(string? Id, string? Title, bool IsTrial, IReadOnlyList<string> ProbeTitles);

/// <param name="Edition">The edition the гурток walks: null for the unnamed one, and also when the
/// one it chose is gone from the catalogue — then its youths are back on the unnamed one.</param>
/// <param name="CanChange">Whether the caller may switch it: the kurin's «Обирати пробу гуртку».</param>
public sealed record GroupProbeEditionDto(
    Guid GroupKey,
    string Name,
    string? Edition,
    bool CanChange,
    IReadOnlyList<GroupMemberStandingDto> Members);

/// <summary>
/// One youth of the гурток and what decides their edition. With nothing started they simply follow
/// the гурток.
/// </summary>
/// <param name="Edition">The edition of <paramref name="ProbeTitle"/>; meaningful only when <paramref name="HasStarted"/>.</param>
/// <param name="IsHeld">They closed <paramref name="ProbeTitle"/>, past the first place: they finish its
/// edition whatever the гурток chooses. Otherwise the probe is only under way and they move with the гурток.</param>
public sealed record GroupMemberStandingDto(
    Guid MemberKey,
    string FullName,
    bool HasStarted,
    string? Edition,
    string? ProbeTitle,
    bool IsHeld);
