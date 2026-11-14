using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AuthModule;

public class InvitationRepository : BaseEntityRepository<Invitation>, IInvitationRepository
{

    public InvitationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await Context.Invitations.FirstOrDefaultAsync(e => e.Token == token && !e.IsRevoked && e.UsedAtUtc == null, cancellationToken);
    }

    public async Task<Invitation?> GetActiveByWaitlistEntryKeyAsync(Guid waitlistEntryKey, CancellationToken cancellationToken = default)
    {
        return await Context.Invitations.FirstOrDefaultAsync(e => e.WaitlistEntryKey == waitlistEntryKey && !e.IsRevoked && e.UsedAtUtc == null, cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> GetActiveForTargetUserAsync(Guid targetUserKey, CancellationToken cancellationToken = default)
    {
        // Tracked on purpose: the caller revokes what comes back in the same save.
        return await Context.Invitations
            .Where(e => e.TargetUserKey == targetUserKey && !e.IsRevoked && e.UsedAtUtc == null)
            .ToListAsync(cancellationToken);
    }

}
