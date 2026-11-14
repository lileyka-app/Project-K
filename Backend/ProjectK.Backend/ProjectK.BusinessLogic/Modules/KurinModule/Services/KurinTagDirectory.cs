using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class KurinTagDirectory : IKurinTagDirectory
{
    private readonly IKurinTagVisibility _visibility;

    public KurinTagDirectory(IKurinTagVisibility visibility)
    {
        _visibility = visibility;
    }

    public async Task<bool> IsEnabledAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => (await _visibility.SettingsAsync(kurinKey, cancellationToken))?.Enabled == true;

    public Task<CapabilityReach> GetCarrierReachAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => _visibility.CarrierReachAsync(kurinKey, cancellationToken);
}
