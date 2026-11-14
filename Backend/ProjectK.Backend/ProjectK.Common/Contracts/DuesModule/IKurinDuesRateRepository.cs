using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Common.Contracts.DuesModule;

public interface IKurinDuesRateRepository : IBaseEntityRepository<KurinDuesRate>
{
    /// <summary>Every rate the kurin has set, oldest first.</summary>
    Task<IReadOnlyList<KurinDuesRate>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
