using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Common.Interfaces.Modules.DuesModule;

public interface IDuesChargeRepository : IBaseEntityRepository<DuesCharge>
{
    /// <summary>Every charge in the kurin.</summary>
    Task<IReadOnlyList<DuesCharge>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Every charge of these memberships, in whichever kurin.</summary>
    Task<IReadOnlyList<DuesCharge>> GetForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default);

    /// <summary>The kurin's charges from <paramref name="fromQuarter"/> on, tracked, for repricing them.</summary>
    Task<IReadOnlyList<DuesCharge>> GetTrackedFromQuarterAsync(Guid kurinKey, int fromQuarter, CancellationToken cancellationToken = default);

    /// <summary>Drops every charge of these memberships — the people behind them are gone. Through the tracker, so it commits with the caller.</summary>
    Task DeleteForMembershipsAsync(IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops a membership's charges from <paramref name="fromQuarter"/> on — the quarters after it
    /// closed. Through the tracker, so it commits with the caller.
    /// </summary>
    Task DeleteFromQuarterAsync(Guid membershipKey, int fromQuarter, CancellationToken cancellationToken = default);
}
