using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Setup;

public sealed record GetSetupStatusQuery() : IRequest<ServiceResult<SetupStatusDto>>;

public sealed class GetSetupStatusQueryHandler : IRequestHandler<GetSetupStatusQuery, ServiceResult<SetupStatusDto>>
{
    private readonly UserManager<AppUser> _userManager;

    public GetSetupStatusQueryHandler(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ServiceResult<SetupStatusDto>> Handle(GetSetupStatusQuery request, CancellationToken cancellationToken)
    {
        var adminUsers = await _userManager.GetUsersInRoleAsync(SystemRoleNames.Admin);
        var isInitialized = adminUsers.Any();
        return new ServiceResult<SetupStatusDto>(ResultType.Success, new SetupStatusDto(isInitialized));
    }
}
