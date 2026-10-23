using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

public interface ILoginResponseFactory
{
    Task<LoginUserResponse> CreateAsync(AppUser user, CancellationToken cancellationToken);
}

public class LoginResponseFactory : ILoginResponseFactory
{
    private readonly IAccessContextResolver _access;
    private readonly IJwtService _jwtService;
    private readonly IMemberDirectory _members;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IPermissionResolver _permissions;

    public LoginResponseFactory(
        IAccessContextResolver access,
        IJwtService jwtService,
        IMemberDirectory members,
        IRefreshTokenStore refreshTokens,
        IPermissionResolver permissions)
    {
        _access = access;
        _jwtService = jwtService;
        _members = members;
        _refreshTokens = refreshTokens;
        _permissions = permissions;
    }

    public async Task<LoginUserResponse> CreateAsync(AppUser user, CancellationToken cancellationToken)
    {
        // What this person may do is answered for the kurin they are in, not for the account.
        // Sign in, refresh and switching kurin all ask the same question here.
        var access = await _access.ResolveAsync(user, cancellationToken);
        var kurinKey = access.KurinKey?.ToString();
        var roles = access.Roles;

        var jwt = new JwtResponse
        {
            AccessToken = _jwtService.GenerateAccessToken(user.Id.ToString(), user.Email!, roles, kurinKey, user.TwoFactorEnabled),
            RefreshToken = _jwtService.GenerateRefreshToken()
        };

        // Adds a session rather than replacing the account's one token: signing in here must not
        // sign the same person out somewhere else.
        await _refreshTokens.IssueAsync(user.Id, jwt.RefreshToken.Token, jwt.RefreshToken.Expires, cancellationToken);

        var member = await _members.FindByAccountAsync(user.Id, cancellationToken);

        // The kurin's own policy, so the client hides exactly what the server would refuse.
        var permissions = await _permissions.ResolveAsync(roles, access.KurinKey, cancellationToken);

        return new LoginUserResponse
        {
            UserKey = user.Id,
            MemberKey = member?.MemberKey,
            Email = user.Email!,
            IsAdmin = access.IsAdmin,
            Permissions = permissions.Select(permission => permission.ToClaimValue()).ToArray(),
            Roles = [.. roles],
            KurinKey = kurinKey,
            RequiresMfa = false,
            Tokens = jwt
        };
    }
}
