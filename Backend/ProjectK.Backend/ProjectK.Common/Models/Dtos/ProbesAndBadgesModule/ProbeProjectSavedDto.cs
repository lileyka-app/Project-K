namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>
/// A saved project: <see cref="ProjectKey"/> so a client can upload into a new one, and the whole
/// «Третя проба» tab after the save.
/// </summary>
public sealed record ProbeProjectSavedDto(Guid ProjectKey, ThirdProbeDto Tab);
