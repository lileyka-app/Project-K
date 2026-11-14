using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ScoreModule;

public class KurinScoreSettingsRepository : BaseEntityRepository<KurinScoreSettings>, IKurinScoreSettingsRepository
{
    public KurinScoreSettingsRepository(AppDbContext context) : base(context)
    {
    }

    public Task<KurinScoreSettings?> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.KurinKey == kurinKey, cancellationToken);
}
