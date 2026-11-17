using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.Common.Contracts.ScoreModule;

public interface IKurinScoreSettingsRepository : IBaseEntityRepository<KurinScoreSettings>
{
    /// <summary>The kurin's settings, tracked — or null while it still scores the default way.</summary>
    Task<KurinScoreSettings?> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
