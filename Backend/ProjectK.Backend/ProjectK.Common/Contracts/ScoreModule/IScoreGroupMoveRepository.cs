using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Common.Contracts.ScoreModule;

public interface IScoreGroupMoveRepository : IBaseEntityRepository<ScoreGroupMove>
{
    /// <summary>Every move between гуртки in the kurin, earliest first, untracked.</summary>
    Task<IReadOnlyList<ScoreGroupMove>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
