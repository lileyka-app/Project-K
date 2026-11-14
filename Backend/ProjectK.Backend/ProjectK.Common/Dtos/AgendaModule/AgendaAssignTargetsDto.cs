namespace ProjectK.Common.Dtos.AgendaModule;

/// <summary>
/// The "Assign for" tree, flagged by what the kurin's policy lets the current user target: each kind of
/// target (the kurin, a гурток, a провід, a person, a tag) has its own row. A гурток is listed when anything
/// in it may be picked.
/// </summary>
public sealed record AgendaAssignTargetsDto
{
    public required bool CanTargetKurin { get; set; }
    public required Guid KurinKey { get; set; }
    public required string KurinLabel { get; set; }

    /// <summary>Kurin-level проводи the viewer may target: КВ and Курінний провід.</summary>
    public required List<AgendaLeadershipTargetDto> KurinLeaderships { get; set; }
    public required List<AgendaGroupTargetDto> Groups { get; set; }

    /// <summary>
    /// People in no гурток and not in the КВ — in a senior kurin, everyone. Listed one by one, so an
    /// item can be aimed at a person a гурток would never reach.
    /// </summary>
    public required List<AgendaMemberTargetDto> UnplacedMembers { get; set; }

    /// <summary>The kurin's tags, when it uses them and the viewer may aim at them; empty otherwise.</summary>
    public required List<AgendaTagTargetDto> Tags { get; set; }
}
