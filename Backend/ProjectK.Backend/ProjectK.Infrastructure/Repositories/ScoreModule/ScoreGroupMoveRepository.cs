using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class ScoreGroupMoveRepository : BaseEntityRepository<ScoreGroupMove>, IScoreGroupMoveRepository
{
    public ScoreGroupMoveRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ScoreGroupMove>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .OrderBy(e => e.MovedAtUtc)
            .ToListAsync(cancellationToken);
}
