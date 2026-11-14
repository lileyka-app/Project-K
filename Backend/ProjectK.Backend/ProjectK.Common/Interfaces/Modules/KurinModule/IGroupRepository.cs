using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IGroupRepository : IBaseEntityRepository<Group>
{
    Task<IEnumerable<Group>> GetAllAsync(Guid kurinKey, CancellationToken token = default);
}
