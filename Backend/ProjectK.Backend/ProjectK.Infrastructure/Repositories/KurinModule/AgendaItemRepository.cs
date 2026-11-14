using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class AgendaItemRepository : BaseEntityRepository<AgendaItem>, IAgendaItemRepository
{

    public AgendaItemRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AgendaItem?> GetByKeyWithAssignmentsAsync(Guid agendaItemKey, CancellationToken token = default)
    {
        return await Context.AgendaItems
            .Include(a => a.Assignments)
            .ThenInclude(a => a.Progress)
            .FirstOrDefaultAsync(a => a.AgendaItemKey == agendaItemKey, token);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetCategoryKeysAsync(IReadOnlyCollection<Guid> agendaItemKeys, CancellationToken token = default)
    {
        if (agendaItemKeys.Count == 0)
        {
            return new Dictionary<Guid, Guid?>();
        }

        return await Context.AgendaItems
            .AsNoTracking()
            .Where(a => agendaItemKeys.Contains(a.AgendaItemKey))
            .ToDictionaryAsync(a => a.AgendaItemKey, a => a.AgendaCategoryKey, token);
    }

    public void AddProgress(AgendaAssignmentProgress progress)
    {
        Context.AgendaAssignmentProgress.Add(progress);
    }

    public void AddAssignment(AgendaAssignment assignment)
    {
        Context.AgendaAssignments.Add(assignment);
    }

    public void RemoveAssignment(AgendaAssignment assignment)
    {
        Context.AgendaAssignments.Remove(assignment);
    }

    public async Task ClearCategoryAsync(Guid agendaCategoryKey, CancellationToken cancellationToken = default)
    {
        await Context.AgendaItems
            .Where(a => a.AgendaCategoryKey == agendaCategoryKey)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.AgendaCategoryKey, (Guid?)null), cancellationToken);
    }

    public async Task RemoveAssignmentsForTargetsAsync(
        IEnumerable<Guid> targetKeys,
        CancellationToken cancellationToken = default)
    {
        var keys = targetKeys.Distinct().ToList();
        if (keys.Count == 0)
        {
            return;
        }

        // Through the tracker rather than ExecuteDelete: callers run this as one step of a larger
        // delete, and ExecuteDelete would commit on its own, ahead of the SaveChanges that
        // removes the target. A failure after it would leave the assignments already gone.
        var assignments = await Context.AgendaAssignments
            .Where(assignment => keys.Contains(assignment.TargetKey))
            .ToListAsync(cancellationToken);

        Context.AgendaAssignments.RemoveRange(assignments);
    }

    public override Task<IEnumerable<AgendaItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use GetForViewerAsync instead.");
    }

    public async Task<IEnumerable<AgendaItem>> GetForViewerAsync(
        AgendaViewerScope viewer,
        DateTime? fromUtc,
        DateTime? toUtc,
        bool onlyDated,
        AgendaItemKind? kind,
        CancellationToken token = default,
        bool archived = false,
        bool includeKurinSchedules = false)
    {
        var query = Context.AgendaItems
            .Where(a => a.KurinKey == viewer.KurinKey);
        query = archived
            ? query.Where(a => a.ArchivedAtUtc != null)
            : query.Where(a => a.ArchivedAtUtc == null);

        if (kind.HasValue)
        {
            query = query.Where(a => a.Kind == kind.Value);
        }

        if (onlyDated)
        {
            query = query.Where(a => a.StartUtc != null);
        }

        // A dated item overlaps the window when it starts before the window ends and finishes
        // after it begins; a single-day item (no EndUtc) is treated as ending at its start. Recurring
        // items skip this narrowing — their base date may sit outside the window while occurrences fall
        // inside — so the handler's expansion decides which instances land in range. We still drop a
        // recurring series once its recurrence-end is before the window, so ended series aren't fetched.
        if (fromUtc.HasValue)
        {
            query = query.Where(a =>
                (a.RecurrenceFrequency != RecurrenceFrequency.None && (a.RecurrenceEndUtc == null || a.RecurrenceEndUtc >= fromUtc.Value))
                || (a.RecurrenceFrequency == RecurrenceFrequency.None && ((a.EndUtc ?? a.StartUtc) == null || (a.EndUtc ?? a.StartUtc) >= fromUtc.Value)));
        }

        if (toUtc.HasValue)
        {
            // Occurrences (recurring or not) never start before the base start, so a series/item starting
            // after the window ends has nothing to show.
            query = query.Where(a => a.StartUtc == null || a.StartUtc <= toUtc.Value);
        }

        if (!viewer.CanSeeWholeKurin)
        {
            query = query.Where(includeKurinSchedules
                ? AgendaVisibility.AssignedOrOnKurinSchedule(viewer)
                : AgendaVisibility.AssignedToViewer(viewer));
        }

        return await query
            .Include(a => a.Assignments)
            .ThenInclude(a => a.Progress)
            .AsNoTracking()
            .OrderBy(a => a.StartUtc)
            .ToListAsync(token);
    }
}
