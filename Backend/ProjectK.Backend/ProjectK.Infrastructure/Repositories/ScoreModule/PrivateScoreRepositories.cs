using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
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

public class PrivateScoreEntryRepository : BaseEntityRepository<PrivateScoreEntry>, IPrivateScoreEntryRepository
{
    public PrivateScoreEntryRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<PrivateScoreEntry>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey && e.DeletedAtUtc == null)
            .OrderBy(e => e.OccurredOn)
            .ThenBy(e => e.CreatedDate)
            .ToListAsync(cancellationToken);

    /// <summary>The КВ's book keeps its pages: deleting is a mark, made by the handler.</summary>
    public override void Delete(PrivateScoreEntry entity, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Private score entries are marked deleted, never removed.");
}
