namespace ProjectK.Common.Models.Dtos.DuesModule;

/// <summary>A гурток whose box the caller may open.</summary>
public sealed record DuesGroupLinkDto
{
    public required Guid GroupKey { get; init; }
    public required string GroupName { get; init; }
}
