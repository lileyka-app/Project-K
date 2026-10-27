namespace ProjectK.Common.Models.Records;

/// <summary>
/// Where a person stands in the programme: <paramref name="Edition"/> is null for the unnamed edition.
/// </summary>
/// <param name="ProbeTitle">The probe that places them, as the catalogue names it.</param>
/// <param name="IsHeld">True when they closed it and it lies past the first place: they finish its
/// edition. False when it is only under way: they move with their гурток, and the probe stops
/// showing in their line while its signatures stay in the Журнал дій.</param>
public sealed record ProbeEditionHold(Guid MemberKey, string? Edition, string ProbeTitle, bool IsHeld);
