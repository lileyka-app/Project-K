using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <inheritdoc />
public sealed class AgendaDirectory : IAgendaDirectory
{
    private readonly IUnitOfWork _uow;
    private readonly IMembershipDirectory _memberships;
    private readonly IOfficeDirectory _offices;
    private readonly IKurinTagDirectory _tags;

    public AgendaDirectory(IUnitOfWork uow, IMembershipDirectory memberships, IOfficeDirectory offices, IKurinTagDirectory tags)
    {
        _uow = uow;
        _memberships = memberships;
        _offices = offices;
        _tags = tags;
    }

    public async Task<IReadOnlyList<AgendaViewerItem>> GetTasksForViewerAsync(
        AgendaViewerContext viewer,
        CancellationToken cancellationToken = default)
    {
        var scope = viewer.ToScope();
        var items = (await _uow.AgendaItems.GetForViewerAsync(scope, null, null, onlyDated: false, kind: AgendaItemKind.Task, cancellationToken)).ToList();
        var roster = await AgendaRosterRules.LoadAsync(_memberships, _offices, _tags, viewer.KurinKey, items, cancellationToken);
        return items.Select(item => ToViewerItem(item, viewer, scope, roster, [])).ToList();
    }

    public async Task<IReadOnlyList<AgendaViewerItem>> GetInWindowForViewerAsync(
        AgendaViewerContext viewer,
        AgendaWindow window,
        CancellationToken cancellationToken = default)
    {
        var scope = viewer.ToScope();
        var items = (await _uow.AgendaItems.GetForViewerAsync(
                scope, window.FromUtc, window.ToUtc, onlyDated: true, kind: window.Kind,
                cancellationToken, includeKurinSchedules: window.IncludeKurinSchedules))
            .ToList();
        if (items.Count == 0)
        {
            return [];
        }

        var tasks = items.Where(item => item.Kind == AgendaItemKind.Task).ToList();
        var roster = tasks.Count > 0
            ? await AgendaRosterRules.LoadAsync(_memberships, _offices, _tags, viewer.KurinKey, tasks, cancellationToken)
            : AgendaRoster.Empty;

        // The person's answers for the whole window in one read, then matched per occurrence: a
        // series' answers are keyed by occurrence start, a one-off's by null.
        var answers = new Dictionary<(Guid, DateTime?), AgendaRsvpStatus>();
        if (window.WithAnswers && viewer.ViewerUserKey is { } userKey)
        {
            var eventKeys = items.Where(item => item.Kind == AgendaItemKind.Event).Select(item => item.AgendaItemKey).ToList();
            answers = (await _uow.AgendaResponses.GetForUserAsync(userKey, eventKeys, window.FromUtc, window.ToUtc, cancellationToken))
                .ToDictionary(r => (r.AgendaItemKey, r.OccurrenceStartUtc), r => r.Status);
        }

        return items
            .Select(item => ToViewerItem(item, viewer, scope, roster, item.Kind == AgendaItemKind.Event
                ? AgendaRecurrenceRules.Expand(item, window.FromUtc, window.ToUtc, window.Zone)
                    .Select(occurrence =>
                    {
                        var key = AgendaOccurrenceRules.KeyOf(item, occurrence.StartUtc);
                        AgendaRsvpStatus? answer = answers.TryGetValue((item.AgendaItemKey, key), out var status) ? status : null;
                        return new AgendaViewerOccurrence(occurrence.StartUtc, occurrence.EndUtc, key, answer);
                    })
                    .ToList()
                : []))
            .ToList();
    }

    public async Task<IReadOnlyList<AgendaCategorySummary>> GetCategoriesAsync(
        Guid kurinKey,
        bool includeArchived,
        CancellationToken cancellationToken = default)
        => (await _uow.AgendaCategories.GetForKurinAsync(kurinKey, includeArchived, cancellationToken))
            .Select(ToSummary)
            .ToList();

    public async Task<AgendaCategorySummary?> FindCategoryAsync(Guid agendaCategoryKey, CancellationToken cancellationToken = default)
        => await _uow.AgendaCategories.GetByKeyAsync(agendaCategoryKey, cancellationToken) is { } category
            ? ToSummary(category)
            : null;

    public async Task<AgendaEventSummary?> FindEventAsync(Guid agendaItemKey, CancellationToken cancellationToken = default)
        => await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(agendaItemKey, cancellationToken) is { Kind: AgendaItemKind.Event } item
            ? ToEvent(item)
            : null;

    public async Task<AgendaEventOccurrence?> FindOccurrenceAsync(
        Guid kurinKey,
        Guid agendaItemKey,
        DateTime occurrenceStartUtc,
        TimeZoneInfo zone,
        CancellationToken cancellationToken = default)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(agendaItemKey, cancellationToken);
        if (item is null || item.KurinKey != kurinKey || item.Kind != AgendaItemKind.Event || !item.StartUtc.HasValue)
        {
            return null;
        }

        var wanted = DateTime.SpecifyKind(occurrenceStartUtc, DateTimeKind.Utc);
        var occurrence = AgendaRecurrenceRules.Expand(item, wanted.AddDays(-1), wanted.AddDays(1), zone)
            .FirstOrDefault(o => DateTime.SpecifyKind(o.StartUtc, DateTimeKind.Utc) == wanted);
        if (occurrence == default)
        {
            return null;
        }

        DateTime? endUtc = occurrence.EndUtc is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null;
        return new AgendaEventOccurrence(ToEvent(item), wanted, endUtc, AgendaOccurrenceRules.KeyOf(item, wanted));
    }

    public async Task<IReadOnlyDictionary<Guid, AgendaRsvpStatus>> GetAnswersAsync(
        Guid agendaItemKey,
        DateTime? occurrenceKey,
        CancellationToken cancellationToken = default)
        => (await _uow.AgendaResponses.GetForItemAsync(agendaItemKey, occurrenceKey, cancellationToken))
            .ToDictionary(r => r.UserKey, r => r.Status);

    public Task<IReadOnlyDictionary<Guid, Guid?>> GetCategoryKeysAsync(
        IReadOnlyCollection<Guid> agendaItemKeys,
        CancellationToken cancellationToken = default)
        => _uow.AgendaItems.GetCategoryKeysAsync(agendaItemKeys, cancellationToken);

    private static AgendaViewerItem ToViewerItem(
        AgendaItem item,
        AgendaViewerContext viewer,
        AgendaViewerScope scope,
        AgendaRoster roster,
        IReadOnlyList<AgendaViewerOccurrence> occurrences)
    {
        var isTask = item.Kind == AgendaItemKind.Task;
        return new AgendaViewerItem(
            AgendaItemKey: item.AgendaItemKey,
            KurinKey: item.KurinKey,
            Kind: item.Kind,
            Title: item.Title,
            Description: item.Description,
            Location: item.Location,
            StartUtc: item.StartUtc,
            EndUtc: item.EndUtc,
            IsAllDay: item.IsAllDay,
            IsRecurring: item.RecurrenceFrequency != RecurrenceFrequency.None,
            AgendaCategoryKey: item.AgendaCategoryKey,
            UpdatedDate: item.UpdatedDate,
            IsAddressedToViewer: AgendaVisibilityRules.IsAddressedTo(item, scope),
            IsByViewer: item.CreatedByUserKey == viewer.ViewerUserKey,
            GroupTargetKeys: item.Assignments.Where(a => a.TargetType == AgendaTargetType.Group).Select(a => a.TargetKey).ToList(),
            ViewerStatus: isTask && AgendaCompletionRules.StakeOf(item, viewer) is not null
                ? AgendaCompletionRules.ViewerStatus(item, viewer, roster)
                : null,
            CanChangeStatus: isTask && AgendaCompletionRules.CanChangeStatus(item, viewer),
            Occurrences: occurrences);
    }

    private static AgendaEventSummary ToEvent(AgendaItem item) => new(
        AgendaItemKey: item.AgendaItemKey,
        KurinKey: item.KurinKey,
        Title: item.Title,
        AgendaCategoryKey: item.AgendaCategoryKey,
        IsAllDay: item.IsAllDay,
        IsRecurring: item.RecurrenceFrequency != RecurrenceFrequency.None,
        IsAimedAtKurin: item.Assignments.Any(a => a.TargetType == AgendaTargetType.Kurin),
        GroupKeys: TargetsOf(item, AgendaTargetType.Group),
        MemberKeys: TargetsOf(item, AgendaTargetType.Member),
        TagKeys: TargetsOf(item, AgendaTargetType.Tag));

    private static HashSet<Guid> TargetsOf(AgendaItem item, AgendaTargetType type) =>
        item.Assignments.Where(a => a.TargetType == type).Select(a => a.TargetKey).ToHashSet();

    private static AgendaCategorySummary ToSummary(AgendaCategory category) => new(
        AgendaCategoryKey: category.AgendaCategoryKey,
        KurinKey: category.KurinKey,
        Name: category.Name,
        ColorHex: category.ColorHex,
        Icon: category.Icon,
        RsvpRequired: category.RsvpRequired,
        IsKurinSchedule: category.IsKurinSchedule,
        DefaultDurationMinutes: category.DefaultDurationMinutes);
}
