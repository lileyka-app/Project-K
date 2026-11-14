namespace ProjectK.Common.Models.Dtos.DuesModule;

public sealed record DuesHandoverDto
{
    public required decimal OwedUp { get; init; }
    public required decimal Transferred { get; init; }
    public required decimal Received { get; init; }
    public required decimal Outstanding { get; init; }
}
