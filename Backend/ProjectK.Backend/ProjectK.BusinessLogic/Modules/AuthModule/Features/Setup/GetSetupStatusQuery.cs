using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

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
