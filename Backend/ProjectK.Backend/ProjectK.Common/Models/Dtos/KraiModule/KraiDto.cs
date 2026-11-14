namespace ProjectK.Common.Models.Dtos.KraiModule;

/// <summary>A край this instance knows, with how many kurins it holds.</summary>
public sealed record KraiDto(string Code, string Name, int KurinCount);
