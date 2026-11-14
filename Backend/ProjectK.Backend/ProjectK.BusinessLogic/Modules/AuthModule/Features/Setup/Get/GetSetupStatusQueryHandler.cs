using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Setup.Get;

public class GetSetupStatusQueryHandler : IRequestHandler<GetSetupStatusQuery, ServiceResult<SetupStatusResponse>>
{
    private readonly UserManager<AppUser> _userManager;

    public GetSetupStatusQueryHandler(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ServiceResult<SetupStatusResponse>> Handle(GetSetupStatusQuery request, CancellationToken cancellationToken)
    {
        var adminUsers = await _userManager.GetUsersInRoleAsync(SystemRole.Admin);
        var isInitialized = adminUsers.Any();
        return new ServiceResult<SetupStatusResponse>(ResultType.Success, new SetupStatusResponse(isInitialized));
    }
}
