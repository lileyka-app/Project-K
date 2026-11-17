using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;

    public SystemSettingsService(IUnitOfWork unitOfWork, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        return _cache.GetOrCreateAsync(
            BackendCacheRules.SystemSettingReads,
            key,
            async token =>
            {
                var setting = await _unitOfWork.SystemSettings.GetByKeyAsync(key, token);
                return setting?.Value;
            },
            cancellationToken);
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);
        return value == null ? defaultValue : value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await _unitOfWork.SystemSettings.GetByKeyAsync(key, cancellationToken);
        if (setting == null)
        {
            setting = new SystemSetting { Key = key, Value = value, UpdatedAtUtc = DateTime.UtcNow };
            _unitOfWork.SystemSettings.Create(setting, cancellationToken);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAtUtc = DateTime.UtcNow;
            _unitOfWork.SystemSettings.Update(setting, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _cache.InvalidateByPrefix(BackendCacheRules.SystemSettingReads.Prefix);
    }
}
