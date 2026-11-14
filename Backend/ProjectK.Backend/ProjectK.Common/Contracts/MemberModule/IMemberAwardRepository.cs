using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Contracts.MemberModule;

public interface IMemberAwardRepository : IBaseEntityRepository<MemberAward>
{
    Task<IReadOnlyCollection<MemberAward>> GetByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default);
    Task<MemberAward?> GetByMemberAndLevelAsync(Guid memberKey, MemberAwardLevel level, CancellationToken cancellationToken = default);
}
