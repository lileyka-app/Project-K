using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

public interface IProbePointProgressRepository : IBaseEntityRepository<ProbePointProgress>
{
    Task<ProbePointProgress?> GetByMemberProbePointAsync(
        Guid memberKey,
        string probeId,
        string pointId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ProbePointProgress>> GetByMemberAndProbeAsync(
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken = default);

    /// <summary>Every signed point in a kurin, untracked.</summary>
    Task<IReadOnlyList<ProbePointProgress>> GetSignedByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Every point row of these people, signed or not: one query for a whole roster.</summary>
    Task<IEnumerable<ProbePointProgress>> GetByMemberKeysAsync(IEnumerable<Guid> memberKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes these people's signed probe points. No foreign key ties them to the member row, so
    /// deleting a member — or a whole kurin of them — has to ask for this explicitly.
    /// </summary>
    Task DeleteForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default);
}
