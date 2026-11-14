using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Common.Contracts.ScoreModule;

public interface IPrivateScoreCriterionRepository : IBaseEntityRepository<PrivateScoreCriterion>
{
    /// <summary>The КВ's list, archived ones too, untracked.</summary>
    Task<IReadOnlyList<PrivateScoreCriterion>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
