using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Models;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.SavedAccount;

/// <summary>
/// Makes one of the kept accounts the one in use, and keeps the one that was in use instead. No
/// password: the kept session is the credential, exactly as the refresh cookie is.
/// </summary>
public sealed record SwitchSavedAccountCommand(
    Guid UserKey,
    IReadOnlyList<string> SavedTokens,
    IReadOnlyList<string> ActiveTokens) : IRequest<ServiceResult<SwitchedAccount>>;

public sealed class SwitchSavedAccountCommandHandler : IRequestHandler<SwitchSavedAccountCommand, ServiceResult<SwitchedAccount>>
{
    private readonly ISavedSessionList _saved;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILoginResponseService _loginResponses;

    public SwitchSavedAccountCommandHandler(
        ISavedSessionList saved,
        IRefreshTokenStore refreshTokens,
        UserManager<AppUser> userManager,
        ILoginResponseService loginResponses)
    {
        _saved = saved;
        _refreshTokens = refreshTokens;
        _userManager = userManager;
        _loginResponses = loginResponses;
    }

    public async Task<ServiceResult<SwitchedAccount>> Handle(SwitchSavedAccountCommand request, CancellationToken cancellationToken)
    {
        var live = await _saved.ReadAsync(request.SavedTokens, cancellationToken);
        var target = live.LastOrDefault(session => session.UserKey == request.UserKey);
        if (target is null)
        {
            return ServiceResult<SwitchedAccount>.Failure(
                ResultType.NotFound,
                IdentityErrors.AccountNotSaved,
                "This browser holds no live session for that account.");
        }

        var user = await _userManager.FindByIdAsync(target.UserKey.ToString());
        if (user is null || !user.CanSignIn())
        {
            return IdentityErrors.SessionEnded<SwitchedAccount>();
        }

        // Spent before a new one is issued, and only by the call that spent it — the same race the
        // refresh guards against.
        if (!await _refreshTokens.RevokeAsync(target.Token, cancellationToken))
        {
            return IdentityErrors.SessionEnded<SwitchedAccount>();
        }

        var login = await _loginResponses.CreateAsync(user, cancellationToken);

        var saved = await _saved.TidyAsync(
            request.SavedTokens.Where(token => token != target.Token).Concat(request.ActiveTokens),
            user.Id,
            cancellationToken);

        return new ServiceResult<SwitchedAccount>(ResultType.Success, new SwitchedAccount(login, saved));
    }
}
