using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Which of the kurin's people an agenda read needs, so the directory is asked for them and not
/// for everyone. The calendar and the board are the most visited pages, and both used to load the
/// whole kurin to put a name on a few assignments; a write did the same to find who to notify.
/// </summary>
public static class AgendaPeople
{
    /// <summary>
    /// The people the items name: member targets, everyone in a гурток done «кожному окремо» (their
    /// rows are listed), and the accounts of whoever wrote, moved or archived anything. Null when a
    /// whole-kurin target is done «кожному окремо» — then every member has a row and all are needed.
    /// </summary>
    public static MemberSelection? ToLabel(IEnumerable<AgendaItem> items)
    {
        var list = items as IReadOnlyCollection<AgendaItem> ?? items.ToList();
        var assignments = list.SelectMany(item => item.Assignments).ToList();

        // A tag's carriers are read at the time, so like the whole kurin they need every member's row.
        if (assignments.Any(a => a.TargetType is AgendaTargetType.Kurin or AgendaTargetType.Tag && a.CompletionMode == AgendaCompletionMode.PerMember))
        {
            return null;
        }

        return new MemberSelection(
            assignments.Where(a => a.TargetType == AgendaTargetType.Member).Select(a => a.TargetKey).Distinct().ToList(),
            assignments.Where(a => a.TargetType == AgendaTargetType.Group && a.CompletionMode == AgendaCompletionMode.PerMember).Select(a => a.TargetKey).Distinct().ToList(),
            AgendaCreatorNames.AccountsNamedBy(list).ToList());
    }

    /// <summary>
    /// The people an item's assignments reach: the members named and everyone in the гуртки aimed
    /// at. Null when the kurin itself is a target — then everyone is reached.
    /// </summary>
    public static MemberSelection? ToNotify(AgendaItem item)
    {
        if (item.Assignments.Any(a => a.TargetType == AgendaTargetType.Kurin))
        {
            return null;
        }

        return new MemberSelection(
            item.Assignments.Where(a => a.TargetType == AgendaTargetType.Member).Select(a => a.TargetKey).Distinct().ToList(),
            item.Assignments.Where(a => a.TargetType == AgendaTargetType.Group).Select(a => a.TargetKey).Distinct().ToList(),
            []);
    }

    /// <summary>One read either way: the selection, or the whole kurin when the selection is null.</summary>
    public static Task<IReadOnlyCollection<MemberSummary>> ReadAsync(
        IMemberDirectory directory,
        Guid kurinKey,
        MemberSelection? selection,
        CancellationToken cancellationToken)
        => selection is null
            ? directory.GetByKurinAsync(kurinKey, cancellationToken)
            : directory.GetByKurinAsync(kurinKey, selection, cancellationToken);
}
