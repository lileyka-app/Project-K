using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Common.Interfaces.Modules.DuesModule;

public interface IKurinDuesSettingsRepository : IBaseEntityRepository<KurinDuesSettings>
{
    /// <summary>The kurin's settings, tracked — or null while it still pays the default way.</summary>
    Task<KurinDuesSettings?> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
