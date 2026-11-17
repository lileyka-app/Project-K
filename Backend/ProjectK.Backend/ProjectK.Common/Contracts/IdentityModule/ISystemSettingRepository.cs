using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.Common.Contracts.IdentityModule;

public interface ISystemSettingRepository : IListableRepository<SystemSetting>
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken token = default);
}
