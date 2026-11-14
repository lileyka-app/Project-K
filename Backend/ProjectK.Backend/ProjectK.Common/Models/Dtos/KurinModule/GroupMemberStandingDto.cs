namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>
/// One youth of the гурток and what decides their edition. With nothing started they simply follow
/// the гурток.
/// </summary>
/// <param name="MemberKey">The youth.</param>
/// <param name="FullName">Their name as the settings list shows it.</param>
/// <param name="HasStarted">Whether they have any probe under way or closed.</param>
/// <param name="Edition">The edition of <paramref name="ProbeTitle"/>; meaningful only when <paramref name="HasStarted"/>.</param>
/// <param name="ProbeTitle">The probe that decides their edition.</param>
/// <param name="IsHeld">They closed <paramref name="ProbeTitle"/>, past the first place: they finish its
/// edition whatever the гурток chooses. Otherwise the probe is only under way and they move with the гурток.</param>
public sealed record GroupMemberStandingDto(
    Guid MemberKey,
    string FullName,
    bool HasStarted,
    string? Edition,
    string? ProbeTitle,
    bool IsHeld);
