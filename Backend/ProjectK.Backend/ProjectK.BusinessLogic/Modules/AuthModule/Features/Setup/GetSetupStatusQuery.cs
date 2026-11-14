using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Setup;

public record GetSetupStatusQuery() : IRequest<ServiceResult<SetupStatusDto>>;

public class GetSetupStatusQueryHandler : IRequestHandler<GetSetupStatusQuery, ServiceResult<SetupStatusDto>>
{
    private readonly UserManager<AppUser> _userManager;

    public GetSetupStatusQueryHandler(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ServiceResult<SetupStatusDto>> Handle(GetSetupStatusQuery request, CancellationToken cancellationToken)
    {
        var adminUsers = await _userManager.GetUsersInRoleAsync(SystemRole.Admin);
        var isInitialized = adminUsers.Any();
        return new ServiceResult<SetupStatusDto>(ResultType.Success, new SetupStatusDto(isInitialized));
    }
}
