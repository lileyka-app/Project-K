using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Mfa;

/// <summary>
/// Whether the signed-in account has a second factor, and whether it has to. Enforcement is a
/// policy decision, not an account setting: holding a privileged office can make the second factor
/// mandatory for an account that had it switched off.
/// </summary>
public sealed record GetMfaStatusQuery : IRequest<ServiceResult<MfaStatusDto>>;

public sealed class GetMfaStatusQueryHandler : IRequestHandler<GetMfaStatusQuery, ServiceResult<MfaStatusDto>>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly UserManager<AppUser> _userManager;
    private readonly IMfaEnforcementReader _policy;

    public GetMfaStatusQueryHandler(
        ICurrentUserContext currentUser,
        UserManager<AppUser> userManager,
        IMfaEnforcementReader policy)
    {
        _currentUser = currentUser;
        _userManager = userManager;
        _policy = policy;
    }

    public async Task<ServiceResult<MfaStatusDto>> Handle(GetMfaStatusQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey)
        {
            return IdentityErrors.AccountMissing<MfaStatusDto>();
        }

        var user = await _userManager.FindByIdAsync(userKey.ToString());
        if (user is null)
        {
            return ServiceResult<MfaStatusDto>.Failure(ResultType.NotFound, IdentityErrors.UserNotFound, "User not found.");
        }

        var isPrivileged = RolePermissionRules.GrantsWholeKurinManagement(_currentUser.Roles ?? []);
        var isMfaRequired = isPrivileged && await _policy.IsPrivilegedMfaRequiredAsync(cancellationToken);

        return new ServiceResult<MfaStatusDto>(
            ResultType.Success,
            new MfaStatusDto(user.TwoFactorEnabled, isMfaRequired));
    }
}
