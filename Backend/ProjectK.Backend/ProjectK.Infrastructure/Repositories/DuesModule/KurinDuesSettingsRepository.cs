using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.DuesModule;

public class KurinDuesSettingsRepository : BaseEntityRepository<KurinDuesSettings>, IKurinDuesSettingsRepository
{
    public KurinDuesSettingsRepository(AppDbContext context) : base(context)
    {
    }

    public Task<KurinDuesSettings?> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.KurinKey == kurinKey, cancellationToken);
}
