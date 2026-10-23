using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.SavedAccount.Get;

/// <summary>
/// The other accounts this browser is signed in to. The cookies are the credential, as for a
/// refresh: whoever holds a session may see whose it is, and nobody else can name one.
/// </summary>
public record GetSavedAccountsQuery(IReadOnlyList<string> SavedTokens, IReadOnlyList<string> ActiveTokens)
    : IRequest<ServiceResult<SavedAccounts>>;

public class GetSavedAccountsQueryHandler : IRequestHandler<GetSavedAccountsQuery, ServiceResult<SavedAccounts>>
{
    private readonly ISavedSessionList _saved;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAccessContextResolver _access;
    private readonly IMemberDirectory _members;
    private readonly IBlobReadLinks _links;

    public GetSavedAccountsQueryHandler(
        ISavedSessionList saved,
        IRefreshTokenStore refreshTokens,
        UserManager<AppUser> userManager,
        IAccessContextResolver access,
        IMemberDirectory members,
        IBlobReadLinks links)
    {
        _saved = saved;
        _refreshTokens = refreshTokens;
        _userManager = userManager;
        _access = access;
        _members = members;
        _links = links;
    }

    public async Task<ServiceResult<SavedAccounts>> Handle(GetSavedAccountsQuery request, CancellationToken cancellationToken)
    {
        var activeUsers = new HashSet<Guid>();
        foreach (var token in request.ActiveTokens.Distinct(StringComparer.Ordinal))
        {
            if (await _refreshTokens.FindActiveAsync(token, cancellationToken) is { } session)
            {
                activeUsers.Add(session.UserId);
            }
        }

        var live = await _saved.ReadAsync(request.SavedTokens, cancellationToken);
        var accounts = new List<SavedAccountDto>();
        var listed = new HashSet<Guid>(activeUsers);

        foreach (var userKey in live.Reverse().Select(session => session.UserKey))
        {
            if (!listed.Add(userKey))
            {
                continue;
            }

            // A suspended account keeps its row until the session expires, but it cannot be switched to.
            var user = await _userManager.FindByIdAsync(userKey.ToString());
            if (user is null || !user.CanSignIn())
            {
                continue;
            }

            var access = await _access.ResolveAsync(user, cancellationToken);
            var member = await _members.FindByAccountAsync(user.Id, cancellationToken);
            accounts.Add(new SavedAccountDto
            {
                UserKey = user.Id,
                MemberKey = member?.MemberKey,
                Email = user.Email ?? string.Empty,
                FullName = member?.FullName ?? string.Empty,
                ProfilePhotoUrl = _links.For(member?.ProfilePhotoBlobName),
                IsAdmin = access.IsAdmin,
                Roles = [.. access.Roles]
            });
        }

        accounts.Reverse();
        return new ServiceResult<SavedAccounts>(ResultType.Success, new SavedAccounts(accounts, live));
    }
}
