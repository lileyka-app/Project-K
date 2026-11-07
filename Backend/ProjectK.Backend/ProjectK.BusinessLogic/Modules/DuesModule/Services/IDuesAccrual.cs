using ProjectK.Common.Models.Dues;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// Writes the quarters of вкладка that have fallen due and are not charged yet. Idempotent: running it
/// twice charges nothing twice, so it is run before every read of a kurin's dues.
/// </summary>
public interface IDuesAccrual
{
    /// <summary>
    /// Charges every youth membership in a гурток of the kurin — every membership, in a senior kurin —
    /// up to the current quarter.
    /// </summary>
    Task AccrueKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges one membership up to the current quarter in the гурток given — the one it is leaving,
    /// so the quarters spent there stay that гурток's.
    /// </summary>
    Task AccrueMembershipAsync(Guid kurinKey, Guid membershipKey, Guid groupKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// In a senior kurin, prices every quarter from <paramref name="from"/> on that is not paid in full
    /// at the rate of the person's tags as they are now — after the kurin's rates changed from it.
    /// </summary>
    Task RepriceKurinAsync(Guid kurinKey, DuesQuarter from, CancellationToken cancellationToken = default);
}
