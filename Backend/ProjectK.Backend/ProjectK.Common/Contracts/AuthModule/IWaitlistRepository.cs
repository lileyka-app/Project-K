using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Common.Contracts.AuthModule;

public interface IWaitlistRepository : IBaseEntityRepository<WaitlistEntry>
{
    Task<WaitlistEntry?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
