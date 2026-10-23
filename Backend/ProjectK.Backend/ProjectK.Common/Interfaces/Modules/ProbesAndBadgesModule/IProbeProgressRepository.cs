using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;

public interface IProbeProgressRepository : IBaseEntityRepository<ProbeProgress>
{
    Task<ProbeProgress?> GetByMemberAndProbeIdAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProbeProgress>> GetByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>Every проба of these people: one query for a whole roster.</summary>
    Task<IEnumerable<ProbeProgress>> GetByMemberKeysAsync(IEnumerable<Guid> memberKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes these people's probe progress. No foreign key ties it to the member row, so deleting a
    /// member — or a whole kurin of them — has to ask for this explicitly.
    /// </summary>
    Task DeleteForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default);

    /// <summary>Every row earned in a kurin, untracked.</summary>
    Task<IReadOnlyList<ProbeProgress>> GetByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>How many rows one person has here, without reading them.</summary>
    Task<int> CountByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default);
}
