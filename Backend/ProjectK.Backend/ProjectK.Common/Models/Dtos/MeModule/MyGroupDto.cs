using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>A гурток the person belongs to as a youth, or leads, in one of their kurins.</summary>
public sealed record MyGroupDto
{
    public required Guid GroupKey { get; init; }
    public required MyKurinRefDto Kurin { get; init; }
    public required string Name { get; init; }
    public required string? SilhouetteUrl { get; init; }
    /// <summary>The person stands in it as a youth.</summary>
    public required bool IsOwn { get; init; }
    /// <summary>The person leads it — an office in its провід, or a mentor assignment.</summary>
    public required bool IsLed { get; init; }
}
