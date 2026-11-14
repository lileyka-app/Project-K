using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.InfrastructureModule;

namespace ProjectK.Common.Contracts.InfrastructureModule;

public interface ISystemSettingRepository : IBaseEntityRepository<SystemSetting>
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken token = default);
}
