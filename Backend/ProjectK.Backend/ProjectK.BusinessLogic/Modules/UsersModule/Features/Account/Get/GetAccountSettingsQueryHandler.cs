using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.Account.Get;

public class GetAccountSettingsQueryHandler : IRequestHandler<GetAccountSettingsQuery, ServiceResult<AccountSettingsDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IMemberDirectory _members;

    private readonly IAccessContextResolver _access;

    public GetAccountSettingsQueryHandler(UserManager<AppUser> userManager, IMemberDirectory members, IAccessContextResolver access)
    {
        _userManager = userManager;
        _members = members;
        _access = access;
    }

    public async Task<ServiceResult<AccountSettingsDto>> Handle(GetAccountSettingsQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user == null)
        {
            return new ServiceResult<AccountSettingsDto>(ResultType.NotFound);
        }

        var roles = (await _access.ResolveAsync(user, cancellationToken)).Roles;
        var member = await _members.FindByAccountAsync(user.Id, cancellationToken);

        var dto = new AccountSettingsDto(
            user.Id,
            member?.MemberKey,
            user.Email!,
            user.PhoneNumber,
            user.FirstName,
            user.LastName,
            roles.FirstOrDefault()!,
            user.TwoFactorEnabled);

        return new ServiceResult<AccountSettingsDto>(ResultType.Success, dto);
    }
}
