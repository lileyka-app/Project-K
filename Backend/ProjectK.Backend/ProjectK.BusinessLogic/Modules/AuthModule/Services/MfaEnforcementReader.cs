using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

public interface IMfaEnforcementReader
{
    Task<bool> IsPrivilegedMfaRequiredAsync(CancellationToken cancellationToken = default);
}

public class MfaEnforcementReader : IMfaEnforcementReader
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ISystemSettingsService _systemSettings;

    public MfaEnforcementReader(
        IHostEnvironment environment,
        IConfiguration configuration,
        ISystemSettingsService systemSettings)
    {
        _environment = environment;
        _configuration = configuration;
        _systemSettings = systemSettings;
    }

    public async Task<bool> IsPrivilegedMfaRequiredAsync(CancellationToken cancellationToken = default)
    {
        // All bypasses below apply to LAN/test tiers only. Production and Staging ignore them outright,
        // so e.g. a stray E2E__BypassPrivilegedMfa in a deployed env file can never disable privileged MFA.
        var isNonProdEnv = !_environment.IsProduction() && !_environment.IsStaging();

        var isBypassed = isNonProdEnv
            && (_environment.IsDevelopment()
                || _configuration.GetValue<bool>("E2E:BypassPrivilegedMfa", false)
                || (_environment.EnvironmentName == "SelfHost"
                    && !await _systemSettings.GetBoolAsync(
                        SystemSettingNames.EnforcePrivilegedMfa,
                        defaultValue: false,
                        cancellationToken)));

        return !isBypassed;
    }
}
