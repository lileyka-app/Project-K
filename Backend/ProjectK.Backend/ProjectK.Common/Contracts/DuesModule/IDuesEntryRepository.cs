using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Common.Contracts.DuesModule;

public interface IDuesEntryRepository : IBaseEntityRepository<DuesEntry>
{
    /// <summary>Every entry in the kurin that has not been deleted, untracked.</summary>
    Task<IReadOnlyList<DuesEntry>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The live entries of these memberships, tracked with their trail, for marking them deleted together.</summary>
    Task<IReadOnlyList<DuesEntry>> GetForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default);
}
