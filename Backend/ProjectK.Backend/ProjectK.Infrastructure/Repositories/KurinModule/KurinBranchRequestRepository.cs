using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class KurinBranchRequestRepository : BaseEntityRepository<KurinBranchRequest>, IKurinBranchRequestRepository
{
    public KurinBranchRequestRepository(AppDbContext context) : base(context)
    {
    }

    public Task<KurinBranchRequest?> GetPendingForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => Context.KurinBranchRequests
            .FirstOrDefaultAsync(r => r.KurinKey == kurinKey && r.Status == KurinBranchRequestStatus.Pending, cancellationToken);

    public async Task<bool> TrySettleAsync(Guid requestKey, KurinBranchRequestStatus status, Guid? reviewedByUserKey, DateTime reviewedAtUtc, CancellationToken cancellationToken = default)
        => await Context.KurinBranchRequests
            .Where(r => r.KurinBranchRequestKey == requestKey && r.Status == KurinBranchRequestStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.ReviewedByUserKey, reviewedByUserKey)
                    .SetProperty(r => r.ReviewedAtUtc, reviewedAtUtc),
                cancellationToken) == 1;

    public async Task<IReadOnlyList<KurinBranchRequest>> GetPendingAsync(CancellationToken cancellationToken = default)
        => await Context.KurinBranchRequests
            .AsNoTracking()
            .Where(r => r.Status == KurinBranchRequestStatus.Pending)
            .OrderBy(r => r.RequestedAtUtc)
            .ToListAsync(cancellationToken);
}
