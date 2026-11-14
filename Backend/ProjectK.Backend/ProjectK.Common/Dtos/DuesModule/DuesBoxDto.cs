namespace ProjectK.Common.Dtos.DuesModule;

public sealed record DuesBoxDto
{
    public required decimal Cash { get; init; }
    public required decimal Card { get; init; }
    public required decimal Total { get; init; }
    public required decimal ToForward { get; init; }
    /// <summary>Paid for quarters not charged yet; kept out of <see cref="Own"/>.</summary>
    public required decimal Ahead { get; init; }
    public required decimal Own { get; init; }
    public required decimal InTransit { get; init; }
}
