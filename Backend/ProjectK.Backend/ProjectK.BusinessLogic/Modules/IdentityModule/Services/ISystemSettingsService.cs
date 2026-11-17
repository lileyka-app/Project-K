namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

public interface ISystemSettingsService
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default);
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);
}
