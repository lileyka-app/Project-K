using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class PlanningSessionRepository : BaseEntityRepository<PlanningSession>, IPlanningSessionRepository
{
    public PlanningSessionRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.PlanningSessions.AnyAsync(ps => ps.PlanningSessionKey == entityKey, cancellationToken);
    }

    public override async Task<PlanningSession?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.PlanningSessions.FirstOrDefaultAsync(ps => ps.PlanningSessionKey == entityKey, cancellationToken);
    }

    public async Task<PlanningSession?> GetByKeyWithDetailsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.PlanningSessions
                             .Include(ps => ps.Participants)
                                .ThenInclude(p => p.BusyRanges)
                             .FirstOrDefaultAsync(ps => ps.PlanningSessionKey == entityKey, cancellationToken);
    }

    public async Task<IEnumerable<PlanningSession>> GetAllByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        return await Context.PlanningSessions
                             .Where(ps => ps.KurinKey == kurinKey)
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);
    }
}
