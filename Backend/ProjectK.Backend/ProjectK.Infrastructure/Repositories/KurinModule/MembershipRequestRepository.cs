using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class MembershipRequestRepository : BaseEntityRepository<MembershipRequest>, IMembershipRequestRepository
{
    public MembershipRequestRepository(AppDbContext context) : base(context)
    {
    }

    public Task<MembershipRequest?> GetAsync(Guid membershipRequestKey, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(r => r.MembershipRequestKey == membershipRequestKey, cancellationToken);

    public async Task<IReadOnlyList<MembershipRequest>> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(r => r.FromKurinKey == kurinKey || r.ToKurinKey == kurinKey)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MembershipRequest>> GetPendingAsync(Guid memberKey, Guid toKurinKey, CancellationToken cancellationToken = default)
        => await Set
            .Where(r => r.MemberKey == memberKey && r.ToKurinKey == toKurinKey && r.Status == MembershipRequestStatus.Pending)
            .ToListAsync(cancellationToken);

    public Task<int> CountOpenToAnswerAsync(Guid fromKurinKey, DateTime nowUtc, CancellationToken cancellationToken = default)
        => Set.CountAsync(
            r => r.FromKurinKey == fromKurinKey && r.Status == MembershipRequestStatus.Pending && r.ExpiresAtUtc > nowUtc,
            cancellationToken);

    public async Task<IReadOnlyList<MembershipRequest>> GetPendingFromAsync(Guid memberKey, Guid fromKurinKey, CancellationToken cancellationToken = default)
        => await Set
            .Where(r => r.MemberKey == memberKey && r.FromKurinKey == fromKurinKey && r.Status == MembershipRequestStatus.Pending)
            .ToListAsync(cancellationToken);
}
