namespace ProjectK.Common.Dtos.KraiModule;

/// <summary>One saved revision of a край's profile, as the history list shows it.</summary>
public sealed record KraiProfileRevisionDto(int Revision, DateTime CreatedAtUtc, string? CreatedByName, string? Note);
