using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <param name="Url">A signed link that expires; never store it, store the key.</param>
public sealed record ProbeProjectImageDto(Guid ImageKey, string Url);
