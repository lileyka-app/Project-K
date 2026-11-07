using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IMemberCardShareRepository : IBaseEntityRepository<MemberCardShare>
{
    Task<MemberCardShare?> GetForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    Task<MemberCardShare?> GetByLinkTokenAsync(string linkToken, CancellationToken cancellationToken = default);
}
