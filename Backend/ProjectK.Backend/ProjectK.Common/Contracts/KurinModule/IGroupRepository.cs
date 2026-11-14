using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Common.Contracts.KurinModule;

public interface IGroupRepository : IBaseEntityRepository<Group>
{
    Task<IEnumerable<Group>> GetAllAsync(Guid kurinKey, CancellationToken token = default);
}
