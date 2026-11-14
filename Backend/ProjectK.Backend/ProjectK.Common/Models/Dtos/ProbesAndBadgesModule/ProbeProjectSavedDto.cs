namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <param name="ProjectKey">The project just saved, so a client can upload into a new one.</param>
/// <param name="Tab">The whole «Третя проба» tab after the save.</param>
public sealed record ProbeProjectSavedDto(Guid ProjectKey, ThirdProbeDto Tab);
