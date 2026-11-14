using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <summary>
/// Name lookups used to label agenda assignments and creators. Loaded for the items on the page:
/// only the people they name are read, not the kurin (<see cref="AgendaPeopleRules"/>).
/// </summary>
public sealed record AgendaNames(
    IReadOnlyDictionary<Guid, string> GroupNames,
    IReadOnlyDictionary<Guid, string> MemberNames,
    IReadOnlyDictionary<Guid, string> CreatorNames,
    IReadOnlyDictionary<Guid, string> TargetLabels,
    IReadOnlyDictionary<Guid, AgendaCategory> Categories,
    IReadOnlyDictionary<Guid, Guid?> MemberGroups)
{
    public const string KurinLabel = "Весь курінь";
    public const string KvLabel = "КВ";
    public const string KurinLeadershipLabel = "Курінний провід";
    public const string GroupLeadershipLabel = "Гуртковий провід";

    /// <summary>What an item aimed at a tag says it is for: everyone carrying it.</summary>
    public static string TagLabel(string? tagName) => tagName is null ? "Усі з тегом" : $"Усі з тегом «{tagName}»";

    public static async Task<AgendaNames> LoadAsync(
        IUnitOfWork uow,
        IMemberDirectory directory,
        IOfficeDirectory offices,
        Guid kurinKey,
        IEnumerable<AgendaItem> items,
        CancellationToken cancellationToken)
    {
        var groups = await uow.Groups.GetAllAsync(kurinKey, cancellationToken);
        var members = await AgendaPeopleRules.ReadAsync(directory, kurinKey, AgendaPeopleRules.ToLabel(items), cancellationToken);

        var groupNames = groups.ToDictionary(g => g.GroupKey, g => g.Name);
        var memberNames = members.ToDictionary(m => m.MemberKey, m => m.FullName);

        // Creators are app users; resolve their display name through the linked member record.
        var creatorNames = AgendaCreatorNames.FromMembers(members);

        var leadershipRefs = await offices.GetLeadershipsInKurinAsync(kurinKey, cancellationToken);
        // Проводи and tags are named by the kurin, not by a person or a гурток; one table labels both.
        var targetLabels = leadershipRefs.ToDictionary(
            r => r.LeadershipKey,
            r => LabelFor(r.Type, r.GroupKey, groupNames));
        foreach (var tag in await uow.KurinTags.GetForKurinAsync(kurinKey, cancellationToken))
        {
            targetLabels[tag.KurinTagKey] = TagLabel(tag.Name);
        }

        // Include archived groups so historical items still resolve their colour/icon.
        var categories = (await uow.AgendaCategories.GetForKurinAsync(kurinKey, includeArchived: true, cancellationToken))
            .ToDictionary(c => c.AgendaCategoryKey);

        var memberGroups = members.GroupBy(m => m.MemberKey).ToDictionary(g => g.Key, g => g.First().GroupKey);

        return new AgendaNames(groupNames, memberNames, creatorNames, targetLabels, categories, memberGroups);
    }

    public static string LabelFor(LeadershipType type, Guid? groupKey, IReadOnlyDictionary<Guid, string> groupNames) =>
        type switch
        {
            LeadershipType.KV => KvLabel,
            LeadershipType.Kurin => KurinLeadershipLabel,
            LeadershipType.Group when groupKey.HasValue && groupNames.TryGetValue(groupKey.Value, out var name)
                => $"{GroupLeadershipLabel} — {name}",
            _ => GroupLeadershipLabel
        };
}
