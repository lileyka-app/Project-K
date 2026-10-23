using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.UsersModule.Models;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User.Get;

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
            return new ServiceResult<UserDto>(ResultType.NotFound);
        }

        // The kurin comes from membership; the account record's own copy is a snapshot from the
        // day it was opened and is not kept up to date.
        var here = (await _memberships.GetCurrentForAccountAsync(user.Id, cancellationToken)).FirstOrDefault();

        // Only the system role is shown here; offices belong to a kurin and are asked for per kurin.
        var isAdmin = await _userManager.IsInRoleAsync(user, SystemRole.Admin);

        var userDto = new UserDto
        {
            UserId = user.Id,
            KurinKey = here?.KurinKey,
            KurinNumber = here?.KurinNumber,
            Email = user.Email!,
            Role = isAdmin ? SystemRole.Admin : SystemRole.Member,
            TwoFactorEnabled = user.TwoFactorEnabled,
            IsSuspended = !user.CanSignIn(),
            FirstName = user.FirstName,
            LastName = user.LastName
        };

        return new ServiceResult<UserDto>(ResultType.Success, userDto);
    }
}
