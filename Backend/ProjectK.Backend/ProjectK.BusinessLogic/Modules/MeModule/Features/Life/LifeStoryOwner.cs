using FluentValidation;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Life;

/// <summary>The person behind the token, whose life story it is. Nobody else reads or writes it.</summary>
public sealed class LifeStoryOwner
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IMemberDirectory _members;

    public LifeStoryOwner(ICurrentUserContext currentUser, IMemberDirectory members)
    {
        _currentUser = currentUser;
        _members = members;
    }

    /// <summary>Null when no person stands behind the account — an administrator without a card.</summary>
    public async Task<Guid?> MemberKeyAsync(CancellationToken cancellationToken)
        => _currentUser.UserId is { } userKey && userKey != Guid.Empty
            ? (await _members.FindByAccountAsync(userKey, cancellationToken))?.MemberKey
            : null;
}
