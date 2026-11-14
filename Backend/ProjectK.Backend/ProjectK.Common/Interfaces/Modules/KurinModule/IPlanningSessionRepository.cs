using ProjectK.Common.Entities.KurinModule.Planning;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IPlanningSessionRepository : IBaseEntityRepository<PlanningSession>
{
    Task<PlanningSession?> GetByKeyWithDetailsAsync(Guid entityKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<PlanningSession>> GetAllByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
