using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.KurinModule;

public class MemberAwardRepository : BaseEntityRepository<MemberAward>, IMemberAwardRepository
{

    public MemberAwardRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<MemberAward?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.MemberAwards
            .AsTracking()
            .FirstOrDefaultAsync(x => x.MemberAwardKey == entityKey, cancellationToken);
    }

    public override async Task<IEnumerable<MemberAward>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Context.MemberAwards
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.MemberAwards
            .AnyAsync(x => x.MemberAwardKey == entityKey, cancellationToken);
    }

    public override void Update(MemberAward entity, CancellationToken cancellationToken = default) => MarkModified(entity);

    public async Task<IReadOnlyCollection<MemberAward>> GetByMemberKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        return await Context.MemberAwards
            .Where(x => x.MemberKey == memberKey)
            .OrderBy(x => x.Level)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<MemberAward?> GetByMemberAndLevelAsync(Guid memberKey, MemberAwardLevel level, CancellationToken cancellationToken = default)
    {
        return await Context.MemberAwards
            .Where(x => x.MemberKey == memberKey && x.Level == level)
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
