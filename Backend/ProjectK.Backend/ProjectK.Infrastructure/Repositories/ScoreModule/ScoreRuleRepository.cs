using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class ScoreRuleRepository : BaseEntityRepository<ScoreRule>, IScoreRuleRepository
{
    public ScoreRuleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ScoreRule>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .OrderBy(e => e.Source).ThenBy(e => e.Variant).ThenBy(e => e.FromDate)
            .ToListAsync(cancellationToken);
}
