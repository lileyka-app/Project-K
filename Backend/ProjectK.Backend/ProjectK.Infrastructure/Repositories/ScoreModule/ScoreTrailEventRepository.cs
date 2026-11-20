using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class ScoreTrailEventRepository : BaseEntityRepository<ScoreTrailEvent>, IScoreTrailEventRepository
{
    public ScoreTrailEventRepository(AppDbContext context) : base(context)
    {
    }


    /// <summary>The trail is append-only.</summary>
    public override void Delete(ScoreTrailEvent entity, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The score trail is never deleted from.");
}
