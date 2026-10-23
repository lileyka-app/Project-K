using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Common.Interfaces.Modules.ScoreModule;

public interface IScoreNotHeldOccurrenceRepository : IBaseEntityRepository<ScoreNotHeldOccurrence>
{
    /// <summary>Every occurrence in the kurin said not to have taken place, untracked.</summary>
    Task<IReadOnlyList<ScoreNotHeldOccurrence>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The one standing for that occurrence, tracked — or null.</summary>
    Task<ScoreNotHeldOccurrence?> FindAsync(Guid agendaItemKey, DateTime occurrenceStartUtc, CancellationToken cancellationToken = default);
}
