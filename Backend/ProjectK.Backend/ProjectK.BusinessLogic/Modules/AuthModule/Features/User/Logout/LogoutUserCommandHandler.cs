using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Logout;

public class LogoutUserCommandHandler : IRequestHandler<LogoutUserCommand, ServiceResult<object>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IRefreshTokenStore _refreshTokens;

    public LogoutUserCommandHandler(UserManager<AppUser> userManager, IRefreshTokenStore refreshTokens)
    {
        _userManager = userManager;
        _refreshTokens = refreshTokens;
    }

    public async Task<ServiceResult<object>> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserKey))
        {
            return ServiceResult<object>.Failure(
                ResultType.Unauthorized,
                "InvalidToken",
                "Access token is missing or invalid.");
        }
        var user = await _userManager.FindByIdAsync(request.UserKey);
        if (user == null)
        {
            return ServiceResult<object>.Failure(
                ResultType.NotFound,
                "UserNotFound",
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
        return new(
            ResultType.Success,
            "User logged out successfully."
        );
    }
}
