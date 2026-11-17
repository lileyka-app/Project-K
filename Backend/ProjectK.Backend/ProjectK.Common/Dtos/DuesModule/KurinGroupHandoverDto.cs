namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>How one гурток stands with the kurin.</summary>
public sealed record KurinGroupHandoverDto
{
    public required Guid GroupKey { get; init; }
    public required string GroupName { get; init; }

    /// <summary>Collected by the гурток for the станиця, the край and the kurin so far.</summary>
    public required decimal OwedUp { get; init; }
    public required decimal Transferred { get; init; }
    public required decimal Received { get; init; }

    /// <summary>Collected but not handed over yet — still in the гурток's box.</summary>
    public required decimal Outstanding { get; init; }

    /// <summary>Handed over, not yet confirmed by the kurin.</summary>
    public required decimal InTransit { get; init; }
}
