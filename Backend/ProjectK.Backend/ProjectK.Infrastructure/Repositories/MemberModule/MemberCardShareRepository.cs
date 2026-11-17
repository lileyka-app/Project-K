using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.MemberModule;

public class MemberCardShareRepository : BaseEntityRepository<MemberCardShare>, IMemberCardShareRepository
{
    public MemberCardShareRepository(AppDbContext context) : base(context)
    {
    }

    public Task<MemberCardShare?> GetForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.MemberKey == memberKey, cancellationToken);

    public Task<MemberCardShare?> GetByLinkTokenAsync(string linkToken, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(e => e.LinkToken == linkToken, cancellationToken);
}
