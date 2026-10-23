using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AuthModule;

public class DismissedDutyRepository : BaseEntityRepository<DismissedDuty>, IDismissedDutyRepository
{
    public DismissedDutyRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<DismissedDuty>> GetForUserAsync(Guid userKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(d => d.UserKey == userKey)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DismissedDuty>> GetToChangeAsync(Guid userKey, string? dutyKey, CancellationToken cancellationToken = default)
        => await Set
            .Where(d => d.UserKey == userKey && (dutyKey == null || d.DutyKey == dutyKey))
            .ToListAsync(cancellationToken);
}
