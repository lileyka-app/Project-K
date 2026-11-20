using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Common.Contracts.MemberModule;

public interface IMemberCardShareRepository : IBaseEntityRepository<MemberCardShare>
{
    Task<MemberCardShare?> GetForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    Task<MemberCardShare?> GetByLinkTokenAsync(string linkToken, CancellationToken cancellationToken = default);
}
