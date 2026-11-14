namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>A picture of a project.</summary>
/// <param name="Url">A signed link that expires; never store it, store the key.</param>
public sealed record ProbeProjectImageDto(Guid ImageKey, string Url);
