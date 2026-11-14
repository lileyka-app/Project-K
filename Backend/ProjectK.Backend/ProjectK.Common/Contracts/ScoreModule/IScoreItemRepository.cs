using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Common.Contracts.ScoreModule;

public interface IScoreItemRepository : IBaseEntityRepository<ScoreItem>
{
    /// <summary>The kurin's list of positions, archived ones too, untracked.</summary>
    Task<IReadOnlyList<ScoreItem>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
