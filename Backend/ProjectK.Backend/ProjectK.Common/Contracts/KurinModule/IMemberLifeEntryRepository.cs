using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Common.Contracts.KurinModule;

public interface IMemberLifeEntryRepository : IBaseEntityRepository<MemberLifeEntry>
{
    /// <summary>The person's entries, oldest first, untracked.</summary>
    Task<IReadOnlyList<MemberLifeEntry>> GetForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>One of the person's entries to change or remove, tracked; null when it is not theirs.</summary>
    Task<MemberLifeEntry?> GetOwnAsync(Guid memberKey, Guid entryKey, CancellationToken cancellationToken = default);
}
