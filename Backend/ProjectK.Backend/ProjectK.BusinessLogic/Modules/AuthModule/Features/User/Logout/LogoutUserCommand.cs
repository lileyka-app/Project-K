using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Logout;

public class LogoutUserCommand : IRequest<ServiceResult<object>>
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
