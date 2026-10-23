using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

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

    private AgendaTargetNames(
        IReadOnlyDictionary<Guid, string> groups,
        IReadOnlyDictionary<Guid, string> members,
        IReadOnlyDictionary<Guid, string> leaderships)
    {
        _groups = groups;
        _members = members;
        _leaderships = leaderships;
    }

    /// <summary>Reads the names <paramref name="targets"/> need, all of them at once.</summary>
    public static async Task<AgendaTargetNames> LoadAsync(
        IUnitOfWork uow,
        IMemberDirectory directory,
        Guid kurinKey,
        IEnumerable<(AgendaTargetType Type, Guid Key)> targets,
        CancellationToken cancellationToken)
    {
        var list = targets.ToList();

        IReadOnlyDictionary<Guid, string> groups = new Dictionary<Guid, string>();
        if (list.Any(target => target.Type is AgendaTargetType.Group or AgendaTargetType.Leadership))
        {
            groups = (await uow.Groups.GetAllAsync(kurinKey, cancellationToken))
                .ToDictionary(group => group.GroupKey, group => group.Name);
        }

        IReadOnlyDictionary<Guid, string> members = new Dictionary<Guid, string>();
        var memberKeys = list.Where(target => target.Type == AgendaTargetType.Member).Select(target => target.Key).Distinct().ToList();
        if (memberKeys.Count > 0)
        {
            members = (await directory.GetByKurinAsync(kurinKey, new MemberSelection(memberKeys, [], []), cancellationToken))
                .GroupBy(member => member.MemberKey)
                .ToDictionary(found => found.Key, found => found.First().FullName);
        }

        IReadOnlyDictionary<Guid, string> leaderships = new Dictionary<Guid, string>();
        if (list.Any(target => target.Type == AgendaTargetType.Leadership))
        {
            leaderships = (await uow.Leaderships.GetLeadershipRefsForKurinAsync(kurinKey, cancellationToken))
                .ToDictionary(found => found.LeadershipKey, found => AgendaLookups.LabelFor(found.Type, found.GroupKey, groups));
        }

        return new AgendaTargetNames(groups, members, leaderships);
    }

    /// <summary>
    /// The targets as one line — the kurin first, then проводи, гуртки and people, each by name —
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
        AgendaTargetType.Kurin => AgendaLookups.KurinLabel,
        AgendaTargetType.Group => _groups.GetValueOrDefault(key, UnknownGroup),
        AgendaTargetType.Member => _members.GetValueOrDefault(key, UnknownMember),
        AgendaTargetType.Leadership => _leaderships.GetValueOrDefault(key, AgendaLookups.GroupLeadershipLabel),
        _ => type.ToString()
    };

    private static int Order(AgendaTargetType type) => type switch
    {
        AgendaTargetType.Kurin => 0,
        AgendaTargetType.Leadership => 1,
        AgendaTargetType.Group => 2,
        _ => 3
    };
}
