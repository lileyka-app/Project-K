using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <summary>
/// The targets of a task or event named the way the calendar names them, for the kurin's journal.
/// Only what the targets need is read: no гуртки for a task aimed at people, nobody for one aimed
/// at the kurin.
/// </summary>
public sealed class AgendaTargetNames
{
    private const string UnknownGroup = "Гурток";
    private const string UnknownMember = "Людина";

    private readonly IReadOnlyDictionary<Guid, string> _groups;
    private readonly IReadOnlyDictionary<Guid, string> _members;
    private readonly IReadOnlyDictionary<Guid, string> _leaderships;
    private readonly IReadOnlyDictionary<Guid, string> _tags;

    private AgendaTargetNames(
        IReadOnlyDictionary<Guid, string> groups,
        IReadOnlyDictionary<Guid, string> members,
        IReadOnlyDictionary<Guid, string> leaderships,
        IReadOnlyDictionary<Guid, string> tags)
    {
        _groups = groups;
        _members = members;
        _leaderships = leaderships;
        _tags = tags;
    }

    /// <summary>Reads the names <paramref name="targets"/> need, all of them at once.</summary>
    public static async Task<AgendaTargetNames> LoadAsync(
        IMembershipDirectory memberships,
        IOfficeDirectory offices,
        IKurinDirectory kurins,
        IKurinTagDirectory kurinTags,
        Guid kurinKey,
        IEnumerable<(AgendaTargetType Type, Guid Key)> targets,
        CancellationToken cancellationToken)
    {
        var list = targets.ToList();

        IReadOnlyDictionary<Guid, string> groups = new Dictionary<Guid, string>();
        if (list.Any(target => target.Type is AgendaTargetType.Group or AgendaTargetType.Leadership))
        {
            groups = await kurins.GetGroupNamesAsync(kurinKey, cancellationToken);
        }

        IReadOnlyDictionary<Guid, string> members = new Dictionary<Guid, string>();
        var memberKeys = list.Where(target => target.Type == AgendaTargetType.Member).Select(target => target.Key).Distinct().ToList();
        if (memberKeys.Count > 0)
        {
            members = MemberSummaryNames.ByMember((
                await memberships.GetPeopleInKurinAsync(kurinKey, new MemberSelection(memberKeys, [], []), cancellationToken)).Select(person => person.Person));
        }

        IReadOnlyDictionary<Guid, string> leaderships = new Dictionary<Guid, string>();
        if (list.Any(target => target.Type == AgendaTargetType.Leadership))
        {
            leaderships = (await offices.GetLeadershipsInKurinAsync(kurinKey, cancellationToken))
                .ToDictionary(found => found.LeadershipKey, found => AgendaNames.LabelFor(found.Type, found.GroupKey, groups));
        }

        IReadOnlyDictionary<Guid, string> tags = new Dictionary<Guid, string>();
        if (list.Any(target => target.Type == AgendaTargetType.Tag))
        {
            tags = (await kurinTags.GetTagsAsync(kurinKey, cancellationToken))
                .ToDictionary(tag => tag.KurinTagKey, tag => AgendaNames.TagLabel(tag.Name));
        }

        return new AgendaTargetNames(groups, members, leaderships, tags);
    }

    /// <summary>
    /// The targets as one line — the kurin first, then проводи, tags, гуртки and people, each by name —
    /// or null when there are none.
    /// </summary>
    public string? Describe(IEnumerable<(AgendaTargetType Type, Guid Key)> targets)
    {
        var names = targets
            .Distinct()
            .Select(target => (target.Type, Name: NameOf(target.Type, target.Key)))
            .OrderBy(target => Order(target.Type))
            .ThenBy(target => target.Name, StringComparer.Ordinal)
            .Select(target => target.Name)
            .ToList();

        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private string NameOf(AgendaTargetType type, Guid key) => type switch
    {
        AgendaTargetType.Kurin => AgendaNames.KurinLabel,
        AgendaTargetType.Group => _groups.GetValueOrDefault(key, UnknownGroup),
        AgendaTargetType.Member => _members.GetValueOrDefault(key, UnknownMember),
        AgendaTargetType.Leadership => _leaderships.GetValueOrDefault(key, AgendaNames.GroupLeadershipLabel),
        AgendaTargetType.Tag => _tags.GetValueOrDefault(key, AgendaNames.TagLabel(null)),
        _ => type.ToString()
    };

    private static int Order(AgendaTargetType type) => type switch
    {
        AgendaTargetType.Kurin => 0,
        AgendaTargetType.Leadership => 1,
        AgendaTargetType.Tag => 2,
        AgendaTargetType.Group => 3,
        _ => 4
    };
}
