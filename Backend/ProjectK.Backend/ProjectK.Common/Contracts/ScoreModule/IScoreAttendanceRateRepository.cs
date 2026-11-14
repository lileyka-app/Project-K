using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Common.Contracts.ScoreModule;

public interface IScoreAttendanceRateRepository : IBaseEntityRepository<ScoreAttendanceRate>
{
    /// <summary>What each group of events and each event is worth in the kurin, untracked.</summary>
    Task<IReadOnlyList<ScoreAttendanceRate>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
