using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Common.Contracts.ScoreModule;

public interface IPrivateScoreEntryRepository : IBaseEntityRepository<PrivateScoreEntry>
{
    /// <summary>Every entry in the kurin that has not been deleted, untracked.</summary>
    Task<IReadOnlyList<PrivateScoreEntry>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
