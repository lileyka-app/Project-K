using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

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
