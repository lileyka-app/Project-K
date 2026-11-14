using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User;

public record GetUserQuery(Guid UserKey) : IRequest<ServiceResult<UserDto>>;

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, ServiceResult<UserDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IMembershipDirectory _memberships;

    public GetUserQueryHandler(UserManager<AppUser> userManager, IMembershipDirectory memberships)
    {
        _userManager = userManager;
        _memberships = memberships;
    }

    public async Task<ServiceResult<UserDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user == null)
        {
            return ServiceResult<UserDto>.Failure(ResultType.NotFound, UsersErrors.UserNotFound, "User not found.");
        }

        // The kurin comes from membership; the account record's own copy is a snapshot from the
        // day it was opened and is not kept up to date.
        var here = (await _memberships.GetCurrentForAccountAsync(user.Id, cancellationToken)).FirstOrDefault();

        // Only the system role is shown here; offices belong to a kurin and are asked for per kurin.
        var isAdmin = await _userManager.IsInRoleAsync(user, SystemRoleNames.Admin);

        var userDto = new UserDto
        {
            UserId = user.Id,
            KurinKey = here?.KurinKey,
            KurinNumber = here?.KurinNumber,
            KurinBranch = null,
            KurinBranches = null,
            KurinNamedAfter = null,
            KurinKraiCode = null,
            Email = user.Email!,
            Role = isAdmin ? SystemRoleNames.Admin : SystemRoleNames.Member,
            TwoFactorEnabled = user.TwoFactorEnabled,
            IsSuspended = !user.CanSignIn(),
            FirstName = user.FirstName,
            LastName = user.LastName
        };

        return new ServiceResult<UserDto>(ResultType.Success, userDto);
    }
}
