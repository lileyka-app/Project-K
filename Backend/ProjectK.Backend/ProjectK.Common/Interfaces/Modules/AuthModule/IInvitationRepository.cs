using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

public interface IInvitationRepository : IBaseEntityRepository<Invitation>
{
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<Invitation?> GetActiveByWaitlistEntryKeyAsync(Guid waitlistEntryKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every invitation that can still activate the given account, whichever queue entry it hangs
    /// off. A resend has to revoke all of them: an account that collected two live tokens over
    /// several resends must not keep one alive after being handed a third.
    /// </summary>
    Task<IReadOnlyList<Invitation>> GetActiveForTargetUserAsync(Guid targetUserKey, CancellationToken cancellationToken = default);
}
