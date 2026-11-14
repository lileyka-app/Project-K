using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class PrivateScoreCriterionRepository : BaseEntityRepository<PrivateScoreCriterion>, IPrivateScoreCriterionRepository
{
    public PrivateScoreCriterionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<PrivateScoreCriterion>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);
}
