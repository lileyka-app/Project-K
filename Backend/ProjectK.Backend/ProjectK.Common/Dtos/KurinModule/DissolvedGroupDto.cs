namespace ProjectK.Common.Dtos.KurinModule;

public sealed record DissolvedGroupDto(Guid GroupKey, string Name, DateOnly DissolvedOn);
