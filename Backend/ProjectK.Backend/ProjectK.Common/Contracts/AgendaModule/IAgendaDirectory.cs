using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.Common.Contracts.AgendaModule;

/// <summary>
/// What the agenda module tells others about its items. A task's state for one person is worked out
/// against the people its targets stand for now, and only the agenda knows how to read them.
/// </summary>
public interface IAgendaDirectory
{
    /// <summary>
    /// The people the items' targets stand for now: the members they name, everyone in a гурток or the
    /// kurin where the items need them, the holders of every провід and the carriers of every tag they
    /// aim at «кожному окремо».
    /// </summary>
    Task<AgendaRoster> GetRosterAsync(
        Guid kurinKey,
        IReadOnlyCollection<AgendaItem> items,
        CancellationToken cancellationToken = default);
}
