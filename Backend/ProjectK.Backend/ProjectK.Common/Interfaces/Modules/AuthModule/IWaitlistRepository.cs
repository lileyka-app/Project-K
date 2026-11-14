using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

public interface IWaitlistRepository : IBaseEntityRepository<WaitlistEntry>
{
    Task<WaitlistEntry?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
