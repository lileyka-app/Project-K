using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class KurinCardSharingRepository : BaseEntityRepository<KurinCardSharing>, IKurinCardSharingRepository
{
    public KurinCardSharingRepository(AppDbContext context) : base(context)
    {
    }

    public Task<KurinCardSharing?> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.KurinKey == kurinKey, cancellationToken);

    public async Task<IReadOnlyCollection<KurinCardSharing>> GetForKurinsAsync(IReadOnlyCollection<Guid> kurinKeys, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Where(e => kurinKeys.Contains(e.KurinKey)).ToListAsync(cancellationToken);
}
