using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Common.Contracts.KurinModule;

public interface IKurinCardSharingRepository : IBaseEntityRepository<KurinCardSharing>
{
    Task<KurinCardSharing?> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The rows of these kurins; a kurin without one is absent.</summary>
    Task<IReadOnlyCollection<KurinCardSharing>> GetForKurinsAsync(IReadOnlyCollection<Guid> kurinKeys, CancellationToken cancellationToken = default);
}
