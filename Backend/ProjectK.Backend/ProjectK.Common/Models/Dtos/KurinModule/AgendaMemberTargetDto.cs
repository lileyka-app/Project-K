using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

public sealed record AgendaMemberTargetDto
{
    public required Guid MemberKey { get; set; }
    public required string FullName { get; set; }

    /// <summary>False when the person is listed only beside the гурток or провід the viewer may pick.</summary>
    public required bool CanTarget { get; set; }
}
