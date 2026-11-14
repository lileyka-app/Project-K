namespace ProjectK.Common.Dtos.AgendaModule;

public sealed record AgendaLeadershipTargetDto
{
    public required Guid LeadershipKey { get; set; }
    public required string Label { get; set; }
    public required bool CanTarget { get; set; }

    /// <summary>
    /// The КВ's members who are in no гурток, to pick one by one; a member placed in a гурток is
    /// picked there. Empty for every other провід.
    /// </summary>
    public required List<AgendaMemberTargetDto> Members { get; set; }
}
