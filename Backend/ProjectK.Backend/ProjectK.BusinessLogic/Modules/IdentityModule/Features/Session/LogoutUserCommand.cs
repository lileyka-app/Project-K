using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Session;

public sealed class LogoutUserCommand : IRequest<ServiceResult<Unit>>
{
    public string? UserKey { get; set; }

    /// <summary>
    /// Every refresh token this browser sent. An account can be signed in in several places, so
    /// signing out here must not sign the person out of the others.
    /// <para>
    /// A list, not one value: a browser can carry more than one <c>refreshToken</c> cookie — a
    /// stale one from an earlier session shadowing the live one — and reading only the first is
    /// how logout came to revoke the wrong session and leave the live one usable. Refresh has
    /// always walked all of them; so does this.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> RefreshTokens { get; set; }

    /// <summary>
    /// The sessions this browser keeps for the other accounts it can switch to. Signing out ends
    /// them too, whoever they belong to: holding the token is the right to end it.
    /// </summary>
    public IReadOnlyList<string> SavedTokens { get; set; }

    public LogoutUserCommand(string? userKey, IReadOnlyList<string>? refreshTokens = null, IReadOnlyList<string>? savedTokens = null)
    {
        UserKey = userKey;
        RefreshTokens = refreshTokens ?? [];
        SavedTokens = savedTokens ?? [];
    }
}

public sealed class LogoutUserCommandHandler : IRequestHandler<LogoutUserCommand, ServiceResult<Unit>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IRefreshTokenStore _refreshTokens;

    public LogoutUserCommandHandler(UserManager<AppUser> userManager, IRefreshTokenStore refreshTokens)
    {
        _userManager = userManager;
        _refreshTokens = refreshTokens;
    }

    public async Task<ServiceResult<Unit>> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserKey))
        {
            return ServiceResult<Unit>.Failure(
                ResultType.Unauthorized,
                IdentityErrors.InvalidToken,
                "Access token is missing or invalid.");
        }
        var user = await _userManager.FindByIdAsync(request.UserKey);
        if (user == null)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.NotFound,
                IdentityErrors.UserNotFound,
                "User not found.");
        }
        // Only this browser's sessions. Signing out on one device leaves the others signed in —
        // the whole point of a row per session.
        foreach (var refreshToken in request.RefreshTokens.Concat(request.SavedTokens).Distinct(StringComparer.Ordinal))
        {
            await _refreshTokens.RevokeAsync(refreshToken, cancellationToken);
        }

        if (request.RefreshTokens.Count == 0)
        {
            // Nothing named the session, so nothing can be ended precisely. Ending all of them is
            // what logout meant before sessions were rows, and it is the safe way to be wrong:
            // the alternative is answering "logged out" while leaving the session usable.
            await _refreshTokens.RevokeAllAsync(user.Id, cancellationToken);
        }

        // Otherwise the next sign-in lands the admin inside the kurin they last
        // stepped into, and /panel bounces them straight back out of it.
        user.ActiveKurinKey = null;
        await _userManager.UpdateAsync(user);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
