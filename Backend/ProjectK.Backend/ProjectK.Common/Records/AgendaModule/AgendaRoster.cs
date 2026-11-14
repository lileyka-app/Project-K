using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Records.AgendaModule;

/// <summary>
/// The people each kind of target stands for, as they are now: a гурток's and the kurin's current
/// members, a провід's current office holders, whoever carries a tag now. Read once per request and shared by every item;
/// <see cref="IAgendaDirectory"/> reads one.
/// </summary>
public sealed class AgendaRoster
{
    private readonly IReadOnlyDictionary<Guid, Guid?> _memberGroups;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> _leadershipMembers;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> _tagMembers;

    public AgendaRoster(
        IReadOnlyDictionary<Guid, Guid?> memberGroups,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> leadershipMembers,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>? tagMembers = null)
    {
        _memberGroups = memberGroups;
        _leadershipMembers = leadershipMembers;
        _tagMembers = tagMembers ?? new Dictionary<Guid, IReadOnlyList<Guid>>();
    }

    public static readonly AgendaRoster Empty = new(new Dictionary<Guid, Guid?>(), new Dictionary<Guid, IReadOnlyList<Guid>>());

    /// <summary>The гурток a person stands in, when this roster read them.</summary>
    public Guid? GroupOf(Guid memberKey) => _memberGroups.GetValueOrDefault(memberKey);

    /// <summary>The member keys a target stands for; a member target is that one person.</summary>
    public IReadOnlyCollection<Guid> PeopleIn(AgendaAssignment assignment) => assignment.TargetType switch
    {
        AgendaTargetType.Member => [assignment.TargetKey],
        AgendaTargetType.Group => _memberGroups.Where(m => m.Value == assignment.TargetKey).Select(m => m.Key).ToList(),
        AgendaTargetType.Kurin => _memberGroups.Keys.ToList(),
        AgendaTargetType.Leadership => _leadershipMembers.TryGetValue(assignment.TargetKey, out var holders) ? holders : [],
        AgendaTargetType.Tag => _tagMembers.TryGetValue(assignment.TargetKey, out var carriers) ? carriers : [],
        _ => []
    };
}
