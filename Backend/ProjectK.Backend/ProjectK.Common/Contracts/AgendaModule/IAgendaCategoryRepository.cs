using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;

namespace ProjectK.Common.Contracts.AgendaModule;

public interface IAgendaCategoryRepository : IBaseEntityRepository<AgendaCategory>
{
    /// <summary>Event groups of a kurin; <paramref name="includeArchived"/> adds hidden ones for management.</summary>
    Task<IReadOnlyList<AgendaCategory>> GetForKurinAsync(Guid kurinKey, bool includeArchived, CancellationToken cancellationToken = default);
}
