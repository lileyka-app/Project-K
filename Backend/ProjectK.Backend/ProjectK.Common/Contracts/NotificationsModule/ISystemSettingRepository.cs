using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.NotificationsModule;

namespace ProjectK.Common.Contracts.NotificationsModule;

public interface ISystemSettingRepository : IListableRepository<SystemSetting>
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken token = default);
}
