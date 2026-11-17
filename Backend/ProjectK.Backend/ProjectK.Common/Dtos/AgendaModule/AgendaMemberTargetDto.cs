namespace ProjectK.Common.Dtos.AgendaModule;

public sealed record AgendaMemberTargetDto
{
    public required Guid MemberKey { get; set; }
    public required string FullName { get; set; }

    /// <summary>False when the person is listed only beside the гурток or провід the viewer may pick.</summary>
    public required bool CanTarget { get; set; }
}
