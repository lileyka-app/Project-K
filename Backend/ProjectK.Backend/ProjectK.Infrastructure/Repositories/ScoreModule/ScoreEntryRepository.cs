using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class ScoreEntryRepository : BaseEntityRepository<ScoreEntry>, IScoreEntryRepository
{
    public ScoreEntryRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ScoreEntry>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(e => e.KurinKey == kurinKey && e.DeletedAtUtc == null)
            .OrderBy(e => e.OccurredOn)
            .ThenBy(e => e.CreatedDate)
            .ToListAsync(cancellationToken);

    /// <summary>Points are never removed: deleting is a mark, made by the handler.</summary>
    public override void Delete(ScoreEntry entity, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Score entries are marked deleted, never removed.");
}
