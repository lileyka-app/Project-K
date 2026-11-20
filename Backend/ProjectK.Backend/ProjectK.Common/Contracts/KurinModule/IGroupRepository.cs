using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Common.Contracts.KurinModule;

public interface IGroupRepository : IBaseEntityRepository<Group>
{
    Task<IEnumerable<Group>> GetAllAsync(Guid kurinKey, CancellationToken token = default);

    /// <summary>The гуртки with these keys, dissolved ones included, untracked.</summary>
    Task<IReadOnlyList<Group>> GetByKeysIncludingDissolvedAsync(IReadOnlyCollection<Guid> groupKeys, CancellationToken cancellationToken = default);
}
