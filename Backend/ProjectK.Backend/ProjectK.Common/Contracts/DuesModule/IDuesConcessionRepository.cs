using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Common.Contracts.DuesModule;

public interface IDuesConcessionRepository : IBaseEntityRepository<DuesConcession>
{
    /// <summary>Every пільга switch in the kurin, oldest first.</summary>
    Task<IReadOnlyList<DuesConcession>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Drops every пільга switch of these memberships — the people behind them are gone.</summary>
    Task DeleteForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default);
}
