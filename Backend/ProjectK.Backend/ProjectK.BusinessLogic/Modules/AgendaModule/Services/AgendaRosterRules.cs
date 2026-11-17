using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <summary>Reads the <see cref="AgendaRoster"/> a page of items needs, and nobody it does not.</summary>
public static class AgendaRosterRules
{
    /// <summary>Reads the people the items stand for and the holders of every провід they aim at.</summary>
    public static async Task<AgendaRoster> LoadAsync(
        IMembershipDirectory memberships,
        IOfficeDirectory offices,
        IKurinTagDirectory tags,
        Guid kurinKey,
        IEnumerable<AgendaItem> items,
        CancellationToken cancellationToken)
    {
        var list = items as IReadOnlyCollection<AgendaItem> ?? items.ToList();
        var members = await AgendaPeopleRules.ReadAsync(memberships, kurinKey, AgendaPeopleRules.ToLabel(list), cancellationToken);
        return await LoadAsync(offices, tags, members.GroupBy(m => m.MemberKey).ToDictionary(g => g.Key, g => g.First().GroupKey), list, cancellationToken);
    }

    /// <summary>The same, over a kurin's members already read for the page.</summary>
    public static async Task<AgendaRoster> LoadAsync(
        IOfficeDirectory offices,
        IKurinTagDirectory tags,
        IReadOnlyDictionary<Guid, Guid?> memberGroups,
        IEnumerable<AgendaItem> items,
        CancellationToken cancellationToken)
    {
        var leadershipKeys = items
            .SelectMany(item => item.Assignments)
            .Where(a => a.TargetType == AgendaTargetType.Leadership && a.CompletionMode == AgendaCompletionMode.PerMember)
            .Select(a => a.TargetKey)
            .Distinct()
            .ToList();

        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> leadershipMembers = leadershipKeys.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<Guid>>()
            : await offices.GetHolderMemberKeysAsync(leadershipKeys, cancellationToken);

        var tagKeys = items
            .SelectMany(item => item.Assignments)
            .Where(a => a.TargetType == AgendaTargetType.Tag && a.CompletionMode == AgendaCompletionMode.PerMember)
            .Select(a => a.TargetKey)
            .Distinct()
            .ToList();
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> tagMembers = tagKeys.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<Guid>>()
            : await tags.GetCarriersAsync(tagKeys, cancellationToken);

        return new AgendaRoster(memberGroups, leadershipMembers, tagMembers);
    }
}
