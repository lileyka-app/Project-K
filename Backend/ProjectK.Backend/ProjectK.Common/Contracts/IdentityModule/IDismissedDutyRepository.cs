using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Common.Contracts.IdentityModule;

public interface IDismissedDutyRepository : IBaseEntityRepository<DismissedDuty>
{
    /// <summary>Every duty the account hid, untracked.</summary>
    Task<IReadOnlyList<DismissedDuty>> GetForUserAsync(Guid userKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The account's hidden duties to change or remove, tracked: the one under <paramref name="dutyKey"/>,
    /// or all of them when it is null.
    /// </summary>
    Task<IReadOnlyList<DismissedDuty>> GetToChangeAsync(Guid userKey, string? dutyKey, CancellationToken cancellationToken = default);
}
