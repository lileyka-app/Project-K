namespace ProjectK.Common.Models.Dtos.DuesModule;

public sealed record DuesPersonDto
{
    public required Guid MemberKey { get; init; }
    public required string FullName { get; init; }
}
