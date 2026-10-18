using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Common.Interfaces.Modules.DuesModule;

public interface IDuesConcessionGrantRepository : IBaseEntityRepository<DuesConcessionGrant>
{
    /// <summary>Every пільга of the край given in the kurin, oldest first.</summary>
    Task<IReadOnlyList<DuesConcessionGrant>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The grant tracked, so a change to it is saved.</summary>
    Task<DuesConcessionGrant?> GetForUpdateAsync(Guid grantKey, CancellationToken cancellationToken = default);

    /// <summary>Drops every grant of these memberships — the people behind them are gone.</summary>
    Task DeleteForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default);
}
