using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Common.Contracts.IdentityModule;

public interface IWaitlistRepository : IListableRepository<WaitlistEntry>
{
    Task<WaitlistEntry?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
