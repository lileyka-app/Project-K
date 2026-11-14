using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.Account;

public record GetAccountSettingsQuery(Guid UserKey) : IRequest<ServiceResult<AccountSettingsDto>>;

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
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.NotFound, UsersErrors.UserNotFound, "User not found.");
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
