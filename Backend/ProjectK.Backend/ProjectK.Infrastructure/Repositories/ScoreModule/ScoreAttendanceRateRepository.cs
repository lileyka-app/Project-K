using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class ScoreAttendanceRateRepository : BaseEntityRepository<ScoreAttendanceRate>, IScoreAttendanceRateRepository
{
    public ScoreAttendanceRateRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ScoreAttendanceRate>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .ToListAsync(cancellationToken);
}
