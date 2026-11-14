using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Mfa;

/// <summary>
/// Whether the signed-in account has a second factor, and whether it has to. Enforcement is a
/// policy decision, not an account setting: holding a privileged office can make the second factor
/// mandatory for an account that had it switched off.
/// </summary>
public sealed record GetMfaStatusQuery : IRequest<ServiceResult<MfaStatusResponse>>;

public sealed record MfaStatusResponse(bool IsMfaEnabled, bool IsMfaRequired);

public sealed class GetMfaStatusQueryHandler : IRequestHandler<GetMfaStatusQuery, ServiceResult<MfaStatusResponse>>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly UserManager<AppUser> _userManager;
    private readonly IMfaEnforcementPolicy _policy;

    public GetMfaStatusQueryHandler(
        ICurrentUserContext currentUser,
        UserManager<AppUser> userManager,
        IMfaEnforcementPolicy policy)
    {
        _currentUser = currentUser;
        _userManager = userManager;
        _policy = policy;
    }

    public async Task<ServiceResult<MfaStatusResponse>> Handle(GetMfaStatusQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey)
        {
            return ServiceResult<MfaStatusResponse>.Failure(ResultType.Unauthorized, "Unauthorized", "User not found or unauthorized.");
        }

        var user = await _userManager.FindByIdAsync(userKey.ToString());
        if (user is null)
        {
            return ServiceResult<MfaStatusResponse>.Failure(ResultType.NotFound, "UserNotFound", "User not found.");
        }

        var isPrivileged = RolePermissionMap.GrantsWholeKurinManagement(_currentUser.Roles ?? []);
        var isMfaRequired = isPrivileged && await _policy.IsPrivilegedMfaRequiredAsync(cancellationToken);

        return new ServiceResult<MfaStatusResponse>(
            ResultType.Success,
            new MfaStatusResponse(user.TwoFactorEnabled, isMfaRequired));
    }
}
