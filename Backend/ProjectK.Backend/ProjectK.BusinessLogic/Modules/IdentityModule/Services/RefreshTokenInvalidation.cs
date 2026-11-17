using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Events.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

/// <summary>
/// Ends every session an account holds, then announces it so the access that lives outside sessions
/// (the calendar link, in Agenda) ends too.
/// <para>
/// Used by the changes that must not leave an older session alive: a new password, a confirmed
/// email change, anything touching the second factor, a suspension. It used to be one line clearing
/// the single token column; now that an account can be signed in in several places, "revoke" has to
/// mean all of them, or a change made because of a suspected compromise would leave the intruder
/// logged in.
/// </para>
/// <para>
/// Call it only once the change is stored: the session store commits on its own, and so do the
/// handlers of <see cref="AccountSessionsEnded"/>.
/// </para>
/// </summary>
public class RefreshTokenInvalidation
{
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IDomainEventPublisher _events;

    public RefreshTokenInvalidation(IRefreshTokenStore refreshTokens, IDomainEventPublisher events)
    {
        _refreshTokens = refreshTokens;
        _events = events;
    }

    public Task RevokeRefreshTokenAsync(AppUser user, CancellationToken cancellationToken = default) =>
        RevokeRefreshTokenAsync(user, SessionEndReason.SecurityChange, cancellationToken);

    public async Task RevokeRefreshTokenAsync(AppUser user, SessionEndReason reason, CancellationToken cancellationToken = default)
    {
        await _refreshTokens.RevokeAllAsync(user.Id, cancellationToken);
        await _events.PublishAsync(new AccountSessionsEnded(user.Id, reason), cancellationToken);
    }
}
