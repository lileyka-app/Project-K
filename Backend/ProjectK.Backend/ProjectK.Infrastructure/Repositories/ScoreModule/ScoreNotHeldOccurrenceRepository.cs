using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class ScoreNotHeldOccurrenceRepository : BaseEntityRepository<ScoreNotHeldOccurrence>, IScoreNotHeldOccurrenceRepository
{
    public ScoreNotHeldOccurrenceRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ScoreNotHeldOccurrence>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .ToListAsync(cancellationToken);

    public Task<ScoreNotHeldOccurrence?> FindAsync(Guid agendaItemKey, DateTime occurrenceStartUtc, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.AgendaItemKey == agendaItemKey && e.OccurrenceStartUtc == occurrenceStartUtc, cancellationToken);
}
