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
    private readonly IAccessSnapshotFactory _snapshots;

    public LoginResponseFactory(
        IAccessContextResolver access,
        IJwtService jwtService,
        IMemberDirectory members,
        IRefreshTokenStore refreshTokens,
        IAccessSnapshotFactory snapshots)
    {
        _access = access;
        _jwtService = jwtService;
        _members = members;
        _refreshTokens = refreshTokens;
        _snapshots = snapshots;
    }

    public async Task<LoginUserResponse> CreateAsync(AppUser user, CancellationToken cancellationToken)
    {
        // What this person may do is answered for the kurin they are in, not for the account.
        // Sign in, refresh and switching kurin all ask the same question here.
        var access = await _access.ResolveAsync(user, cancellationToken);

        var jwt = new JwtResponse
        {
            AccessToken = _jwtService.GenerateAccessToken(user.Id.ToString(), user.Email!, access.Roles, access.KurinKey?.ToString(), user.TwoFactorEnabled),
            RefreshToken = _jwtService.GenerateRefreshToken()
        };

        // Adds a session rather than replacing the account's one token: signing in here must not
        // sign the same person out somewhere else.
        await _refreshTokens.IssueAsync(user.Id, jwt.RefreshToken.Token, jwt.RefreshToken.Expires, cancellationToken);

        var member = await _members.FindByAccountAsync(user.Id, cancellationToken);

        var snapshot = await _snapshots.CreateAsync(access, cancellationToken);

        return new LoginUserResponse
        {
            UserKey = user.Id,
            MemberKey = member?.MemberKey,
            Email = user.Email!,
            IsAdmin = snapshot.IsAdmin,
            Permissions = snapshot.Permissions,
            Roles = snapshot.Roles,
            KurinKey = snapshot.KurinKey,
            RequiresMfa = false,
            Tokens = jwt
        };
    }
}
