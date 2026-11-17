using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Contracts.MemberModule;

public interface IMemberWarningRepository : IBaseEntityRepository<MemberWarning>
{
    Task<IReadOnlyCollection<MemberWarning>> GetByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MemberWarning>> GetActiveByMemberKeyAsync(Guid memberKey, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<MemberWarning?> GetActiveByMemberAndLevelAsync(Guid memberKey, MemberWarningLevel level, DateTime nowUtc, CancellationToken cancellationToken = default);
    /// <summary>Every пересторога a kurin issued that stands at <paramref name="nowUtc"/>, untracked.</summary>
    Task<IReadOnlyCollection<MemberWarning>> GetActiveByKurinKeyAsync(Guid kurinKey, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<int> ExpireActiveWarningsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
