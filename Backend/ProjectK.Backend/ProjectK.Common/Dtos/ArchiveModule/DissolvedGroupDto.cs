namespace ProjectK.Common.Dtos.ArchiveModule;

public sealed record DissolvedGroupDto(Guid GroupKey, string Name, DateOnly DissolvedOn);
