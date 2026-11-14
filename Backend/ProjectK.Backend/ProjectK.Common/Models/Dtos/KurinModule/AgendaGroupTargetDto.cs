using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

public sealed record AgendaGroupTargetDto
{
    public required Guid GroupKey { get; set; }
    public required string Name { get; set; }

    /// <summary>False when the group is shown only as a container for its members (no group-level target right).</summary>
    public required bool CanTargetGroup { get; set; }

    /// <summary>The group's Гуртковий провід, when it has an active office; null otherwise.</summary>
    public required AgendaLeadershipTargetDto? Leadership { get; set; }
    public required List<AgendaMemberTargetDto> Members { get; set; }
}
